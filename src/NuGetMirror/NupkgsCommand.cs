using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    internal static class NupkgsCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger log)
        {
            cmdApp.Command("nupkgs", (cmd) => Run(cmd, httpSource, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, HttpSource httpSource, ILogger log)
        {
            cmd.Description = "Mirror nupkgs to a folder.";

            var output = cmd.Option("-o|--output", "Output directory for nupkgs.", CommandOptionType.SingleValue);
            var outputFiles = cmd.Option("--file-list", "Output a list of files added.", CommandOptionType.SingleValue);
            var ignoreErrors = cmd.Option("--ignore-errors", "Ignore errors and continue mirroring packages.", CommandOptionType.NoValue);
            var delay = cmd.Option("--delay", "Delay downloading the latest packages to avoid errors. This value is in minutes. Default: 10", CommandOptionType.SingleValue);
            var maxThreadsOption = cmd.Option("--max-threads", "Maximum number of concurrent downloads. Default: 8", CommandOptionType.SingleValue);

            var argRoot = cmd.Argument(
                "[root]",
                "V3 feed index.json URI",
                multipleValues: false);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(async () =>
            {
                var timer = new Stopwatch();
                timer.Start();

                if (string.IsNullOrEmpty(argRoot.Value))
                {
                    throw new ArgumentException("Provide the full http url to a v3 nuget feed.");
                }

                var index = new Uri(argRoot.Value);

                if (!index.AbsolutePath.EndsWith("/index.json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Invalid feed url: '{argRoot.Value}'. Provide the full http url to a v3 nuget feed.");
                }

                // Create root
                var outputPath = Directory.GetCurrentDirectory();

                if (output.HasValue())
                {
                    outputPath = output.Value();
                }

                var delayTime = TimeSpan.FromMinutes(10);

                if (delay.HasValue())
                {
                    if (Int32.TryParse(delay.Value(), out int x))
                    {
                        var delayMinutes = Math.Max(0, x);
                        delayTime = TimeSpan.FromMinutes(delayMinutes);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid --delay value. This must be an integer.");
                    }
                }

                var maxThreads = 8;

                if (maxThreadsOption.HasValue())
                {
                    if (Int32.TryParse(maxThreadsOption.Value(), out int x))
                    {
                        maxThreads = Math.Max(1, x);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid --max-threads value. This must be an integer.");
                    }
                }

                var batchSize = 128;

                var root = new DirectoryInfo(outputPath);
                root.Create();

                FileInfo outputFilesInfo = null;

                if (outputFiles.HasValue())
                {
                    outputFilesInfo = new FileInfo(outputFiles.Value());
                    FileUtility.Delete(outputFilesInfo.FullName);
                }

                var start = MirrorUtility.LoadCursor(root);
                var end = DateTimeOffset.UtcNow.Subtract(delayTime);
                var token = CancellationToken.None;
                var mode = DownloadMode.OverwriteIfNewer;

                // CatalogReader
                using (var cacheContext = new SourceCacheContext())
                using (var catalogReader = new CatalogReader(index, httpSource, cacheContext, TimeSpan.Zero, log))
                {
                    // Find the most recent entry for each package in the range
                    // Order by oldest first
                    var toProcess = new Queue<CatalogEntry>((await catalogReader
                        .GetFlattenedEntriesAsync(start, end, token))
                        .OrderBy(e => e.CommitTimeStamp));

                    var done = new List<CatalogEntry>(128);
                    var complete = 0;
                    var total = toProcess.Count;

                    // Download files
                    var files = new List<string>();
                    var tasks = new List<Task<NupkgResult>>(maxThreads);

                    // Download with throttling
                    while (toProcess.Count > 0)
                    {
                        // Create batches
                        var batch = new Queue<CatalogEntry>(batchSize);

                        while (toProcess.Count > 0 && batch.Count < batchSize)
                        {
                            batch.Enqueue(toProcess.Dequeue());
                        }

                        while (batch.Count > 0)
                        {
                            if (tasks.Count == maxThreads)
                            {
                                await CompleteTaskAsync(files, tasks, done);
                            }

                            var entry = batch.Dequeue();
                            tasks.Add(DownloadNupkgAsync(entry, root.FullName, mode, ignoreErrors.HasValue(), log, token));
                        }

                        // Wait for all batch downloads
                        while (tasks.Count > 0)
                        {
                            await CompleteTaskAsync(files, tasks, done);
                        }

                        complete += done.Count;

                        // Update cursor
                        var newestCommit = GetNewestCommit(done, toProcess);
                        if (newestCommit != null)
                        {
                            log.LogMinimal($"==============================================");
                            log.LogMinimal($"[ {complete} / {total} ] nupkgs processed.");
                            log.LogMinimal($"Most recent commit processed: {newestCommit.Value.ToString("o")}");

                            double rate = complete / Math.Max(1, timer.Elapsed.TotalSeconds);
                            var timeLeft = TimeSpan.FromSeconds(rate * (total - complete));

                            log.LogMinimal($"Estimated time left: {timeLeft}");
                            log.LogMinimal($"==============================================");

                            log.LogMinimal($"Saving {Path.Combine(root.FullName, "cursor.json")}");
                            MirrorUtility.SaveCursor(root, newestCommit.Value);
                        }

                        done.Clear();

                        // Free up space
                        log.LogMinimal($"Clearing tmp http cache");
                        catalogReader.ClearCache();
                    }

                    files = files.Where(e => e != null).ToList();

                    if (outputFilesInfo != null)
                    {
                        // Write out new files
                        using (var writer = new StreamWriter(new FileStream(outputFilesInfo.FullName, FileMode.CreateNew)))
                        {
                            foreach (var file in files)
                            {
                                writer.WriteLine(file);
                            }
                        }
                    }

                    foreach (var file in files)
                    {
                        log.LogMinimal($"new: {file}");
                    }

                    timer.Stop();

                    var plural = files.Count == 1 ? "" : "s";
                    log.LogMinimal($"Downloaded {files.Count} nupkg{plural} in {timer.Elapsed.ToString()}.");
                }

                return 0;
            });
        }

        private static async Task CompleteTaskAsync(List<string> files, List<Task<NupkgResult>> tasks, List<CatalogEntry> done)
        {
            var task = await Task.WhenAny(tasks);
            tasks.Remove(task);
            files.Add(task.Result.Nupkg?.FullName);
            done.Add(task.Result.Entry);
        }

        private static DateTimeOffset? GetNewestCommit(List<CatalogEntry> done, Queue<CatalogEntry> toProcess)
        {
            IEnumerable<CatalogEntry> sorted = done;

            if (toProcess.Count > 0)
            {
                sorted = sorted.Where(e => e.CommitTimeStamp < toProcess.Peek().CommitTimeStamp);
            }

            return sorted.OrderByDescending(e => e.CommitTimeStamp).FirstOrDefault()?.CommitTimeStamp;
        }

        internal static async Task<NupkgResult> DownloadNupkgAsync(CatalogEntry entry, string rootDir, DownloadMode mode, bool ignoreErrors, ILogger log, CancellationToken token)
        {
            // id/version/id.version.nupkg
            var outputDir = Path.Combine(rootDir, entry.Id.ToLowerInvariant(), entry.Version.ToNormalizedString().ToLowerInvariant());

            var result = new NupkgResult()
            {
                Entry = entry
            };

            try
            {
                result.Nupkg = await entry.DownloadNupkgAsync(outputDir, mode, token);
            }
            catch (Exception ex) when (ignoreErrors)
            {
                MirrorUtility.LogExceptionAsWarning(ex, log);
            }

            return result;
        }

        internal class NupkgResult
        {
            public FileInfo Nupkg { get; set; }

            public CatalogEntry Entry { get; set; }
        }
    }
}