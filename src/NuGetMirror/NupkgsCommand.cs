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
using NuGet.Packaging;
using NuGet.Packaging.Core;
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
            var folderFormat = cmd.Option("--folder-format", "Output folder format. Defaults to v3. Options: (v2|v3)", CommandOptionType.SingleValue);
            var stopOnError = cmd.Option("--stop-on-error", "Stop when an error such as a bad or missing package occurs. The default is to warn since this is often expected for nuget.org.", CommandOptionType.NoValue);
            var delay = cmd.Option("--delay", "Avoid downloading the very latest packages on the feed to avoid errors. This value is in minutes. Default: 10", CommandOptionType.SingleValue);
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

                FileInfo outputFilesInfo = new FileInfo(Path.Combine(root.FullName, "updatedFiles.txt"));
                FileUtility.Delete(outputFilesInfo.FullName);

                var useV3Format = true;

                if (folderFormat.HasValue())
                {
                    switch (folderFormat.Value().ToLowerInvariant())
                    {
                        case "v2":
                            useV3Format = false;
                            break;
                        case "v3":
                            useV3Format = true;
                            break;
                        default:
                            throw new ArgumentException($"Invalid {folderFormat.LongName} value: '{folderFormat.Value()}'.");
                    }
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

                            // Queue download task
                            if (useV3Format)
                            {
                                tasks.Add(DownloadNupkgV3Async(entry, root.FullName, mode, stopOnError.HasValue(), log, token));
                            }
                            else
                            {
                                tasks.Add(DownloadNupkgV2Async(entry, root.FullName, mode, stopOnError.HasValue(), log, token));
                            }
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

        internal static async Task<NupkgResult> DownloadNupkgV2Async(CatalogEntry entry, string rootDir, DownloadMode mode, bool stopOnError, ILogger log, CancellationToken token)
        {
            // id/id.version.nupkg 
            var outputDir = Path.Combine(rootDir, entry.Id.ToLowerInvariant());
            var nupkgPath = Path.Combine(outputDir, $"{entry.FileBaseName}.nupkg");

            var result = new NupkgResult()
            {
                Entry = entry
            };

            var lastCreated = DateTimeOffset.MinValue;

            try
            {
                if (File.Exists(nupkgPath))
                {
                    lastCreated = File.GetCreationTimeUtc(nupkgPath);
                }

                // Download
                var nupkgFile = await entry.DownloadNupkgAsync(outputDir, mode, token);

                if (File.Exists(nupkgPath))
                {
                    var currentCreated = File.GetCreationTimeUtc(nupkgPath);

                    // Clean up nuspec and hash if the file changed
                    if (lastCreated < currentCreated)
                    {
                        result.Nupkg = nupkgFile;
                    }
                }
            }
            catch (Exception ex) when (!stopOnError)
            {
                MirrorUtility.LogExceptionAsWarning(ex, log);
            }

            return result;
        }

        internal static async Task<NupkgResult> DownloadNupkgV3Async(CatalogEntry entry, string rootDir, DownloadMode mode, bool stopOnError, ILogger log, CancellationToken token)
        {
            // id/version/id.version.nupkg 
            var versionFolderResolver = new VersionFolderPathResolver(rootDir);
            var outputDir = versionFolderResolver.GetInstallPath(entry.Id, entry.Version);
            var hashPath = versionFolderResolver.GetHashPath(entry.Id, entry.Version);
            var nuspecPath = versionFolderResolver.GetManifestFilePath(entry.Id, entry.Version);
            var nupkgPath = versionFolderResolver.GetPackageFilePath(entry.Id, entry.Version);

            var result = new NupkgResult()
            {
                Entry = entry
            };

            var lastCreated = DateTimeOffset.MinValue;

            try
            {
                if (File.Exists(nupkgPath))
                {
                    lastCreated = File.GetCreationTimeUtc(nupkgPath);
                }

                // Download
                var nupkgFile = await entry.DownloadNupkgAsync(outputDir, mode, token);

                if (File.Exists(nupkgPath))
                {
                    var currentCreated = File.GetCreationTimeUtc(nupkgPath);

                    // Clean up nuspec and hash if the file changed
                    if (lastCreated < currentCreated || !File.Exists(hashPath) || !File.Exists(nuspecPath))
                    {
                        result.Nupkg = nupkgFile;

                        FileUtility.Delete(hashPath);
                        FileUtility.Delete(nuspecPath);

                        using (var fileStream = File.OpenRead(result.Nupkg.FullName))
                        {
                            var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(fileStream));
                            fileStream.Seek(0, SeekOrigin.Begin);

                            // Write nuspec
                            using (var reader = new PackageArchiveReader(fileStream))
                            {
                                var nuspecFile = reader.GetNuspecFile();
                                reader.ExtractFile(nuspecFile, nuspecPath, log);
                            }

                            // Write package hash
                            File.WriteAllText(hashPath, packageHash);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stopOnError)
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