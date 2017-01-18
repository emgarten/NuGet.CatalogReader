using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    /// <summary>
    /// Mirror nupkgs to a folder.
    /// </summary>
    internal static class NupkgsCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger consoleLog)
        {
            cmdApp.Command("nupkgs", (cmd) => Run(cmd, httpSource, consoleLog), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, HttpSource httpSource, ILogger consoleLog)
        {
            cmd.Description = "Mirror nupkgs to a folder.";

            var output = cmd.Option("-o|--output", "Output directory for nupkgs.", CommandOptionType.SingleValue);
            var folderFormat = cmd.Option("--folder-format", "Output folder format. Defaults to v3. Options: (v2|v3)", CommandOptionType.SingleValue);
            var ignoreErrors = cmd.Option("--ignore-errors", "Continue on errors.", CommandOptionType.NoValue);
            var delay = cmd.Option("--delay", "Avoid downloading the very latest packages on the feed to avoid errors. This value is in minutes. Default: 10", CommandOptionType.SingleValue);
            var maxThreadsOption = cmd.Option("--max-threads", "Maximum number of concurrent downloads. Default: 8", CommandOptionType.SingleValue);
            var verbose = cmd.Option("--verbose", "Output additional network information.", CommandOptionType.NoValue);
            var includeIdOption = cmd.Option("-i|--include-id", "Include only these package ids or wildcards. May be provided multiple times.", CommandOptionType.MultipleValue);
            var excludeIdOption = cmd.Option("-e|--exclude-id", "Exclude these package ids or wildcards. May be provided multiple times.", CommandOptionType.MultipleValue);

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
                    throw new ArgumentException($"Invalid feed url: '{argRoot.Value}'. Provide the full http url to a v3 nuget feed. For nuget.org use: https://api.nuget.org/v3/index.json");
                }

                // Create root
                var outputPath = Directory.GetCurrentDirectory();

                if (output.HasValue())
                {
                    outputPath = output.Value();
                }

                var tmpCachePath = Path.Combine(outputPath, ".tmp");

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

                var batchSize = 1000;

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

                var errorLogPath = Path.Combine(outputPath, "lastRunErrors.txt");
                FileUtility.Delete(errorLogPath);

                // Loggers
                // source -> deep -> file -> Console
                var log = new FileLogger(consoleLog, LogLevel.Error, errorLogPath);
                var deepLogger = new FilterLogger(log, LogLevel.Error);

                // Init
                log.LogInformation($"Mirroring {index.AbsoluteUri} -> {outputPath}");

                var formatName = useV3Format ? "{id}/{version}/{id}.{version}.nupkg" : "{id}/{id}.{version}.nupkg";
                log.LogInformation($"Folder format:\t{formatName}");

                log.LogInformation($"Cursor:\t\t{Path.Combine(outputPath, "cursor.json")}");
                log.LogInformation($"Change log:\t{outputFilesInfo.FullName}");
                log.LogInformation($"Error log:\t{errorLogPath}");

                log.LogInformation("Range start:\t" + start.ToString("o"));
                log.LogInformation("Range end:\t" + end.ToString("o"));
                log.LogInformation($"Batch size:\t{batchSize}");
                log.LogInformation($"Threads:\t{maxThreads}");

                // CatalogReader
                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.SetTempRoot(tmpCachePath);

                    using (var catalogReader = new CatalogReader(index, httpSource, cacheContext, TimeSpan.Zero, deepLogger))
                    {
                        // Find the most recent entry for each package in the range
                        // Order by oldest first
                        IEnumerable<CatalogEntry> entryQuery = (await catalogReader
                            .GetFlattenedEntriesAsync(start, end, token));

                        // Remove all but includes if given
                        if (includeIdOption.HasValue())
                        {
                            var regex = includeIdOption.Values.Select(s => MirrorUtility.WildcardToRegex(s)).ToArray();

                            entryQuery = entryQuery.Where(e =>
                                regex.Any(r => r.IsMatch(e.Id)));
                        }

                        // Remove all excludes if given
                        if (excludeIdOption.HasValue())
                        {
                            var regex = excludeIdOption.Values.Select(s => MirrorUtility.WildcardToRegex(s)).ToArray();

                            entryQuery = entryQuery.Where(e =>
                                regex.All(r => !r.IsMatch(e.Id)));
                        }

                        var toProcess = new Queue<CatalogEntry>(entryQuery.OrderBy(e => e.CommitTimeStamp));

                        log.LogInformation($"Catalog entries found: {toProcess.Count}");

                        var done = new List<CatalogEntry>(batchSize);
                        var complete = 0;
                        var total = toProcess.Count;
                        var totalDownloads = 0;

                        // Download files
                        var tasks = new List<Task<NupkgResult>>(maxThreads);

                        // Download with throttling
                        while (toProcess.Count > 0)
                        {
                            // Create batches
                            var batch = new Queue<CatalogEntry>(batchSize);
                            var files = new List<string>();
                            var batchTimer = new Stopwatch();
                            batchTimer.Start();

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
                                    tasks.Add(DownloadNupkgV3Async(entry, root.FullName, mode, ignoreErrors.HasValue(), log, deepLogger, token));
                                }
                                else
                                {
                                    tasks.Add(DownloadNupkgV2Async(entry, root.FullName, mode, ignoreErrors.HasValue(), log, token));
                                }
                            }

                            // Wait for all batch downloads
                            while (tasks.Count > 0)
                            {
                                await CompleteTaskAsync(files, tasks, done);
                            }

                            files = files.Where(e => e != null).ToList();

                            // Write out new files
                            using (var newFileWriter = new StreamWriter(new FileStream(outputFilesInfo.FullName, FileMode.Append, FileAccess.Write)))
                            {
                                foreach (var file in files)
                                {
                                    newFileWriter.WriteLine(file);
                                }
                            }

                            complete += done.Count;
                            totalDownloads += files.Count;
                            batchTimer.Stop();

                            // Update cursor
                            var newestCommit = GetNewestCommit(done, toProcess);
                            if (newestCommit != null)
                            {
                                log.LogMinimal($"================[batch complete]================");
                                log.LogMinimal($"Processed:\t\t{complete} / {total}");
                                log.LogMinimal($"Batch downloads:\t{files.Count}");
                                log.LogMinimal($"Batch time:\t\t{batchTimer.Elapsed}");
                                log.LogMinimal($"Updating cursor.json:\t{newestCommit.Value.ToString("o")}");

                                double rate = complete / Math.Max(1, timer.Elapsed.TotalSeconds);
                                var timeLeft = TimeSpan.FromSeconds(rate * (total - complete));

                                log.LogMinimal($"Estimated time left:\t{timeLeft}");
                                log.LogMinimal($"================================================");

                                MirrorUtility.SaveCursor(root, newestCommit.Value);
                            }

                            done.Clear();

                            // Free up space
                            catalogReader.ClearCache();
                        }

                        // Set cursor to end time
                        MirrorUtility.SaveCursor(root, end);

                        timer.Stop();

                        var plural = totalDownloads == 1 ? "" : "s";
                        log.LogMinimal($"Downloaded {totalDownloads} nupkg{plural} in {timer.Elapsed.ToString()}.");
                    }
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

        internal static async Task<NupkgResult> DownloadNupkgV2Async(CatalogEntry entry, string rootDir, DownloadMode mode, bool ignoreErrors, ILogger log, CancellationToken token)
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
                        log.LogInformation(nupkgFile.FullName);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancelled tasks
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                log.LogWarning($"Unable to download {entry.Id} {entry.Version.ToFullString()} to {nupkgPath}"
                    + Environment.NewLine
                    + MirrorUtility.GetExceptions(ex, "\t- "));
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to download {entry.Id} {entry.Version.ToFullString()} to {nupkgPath}"
                    + Environment.NewLine
                    + MirrorUtility.GetExceptions(ex, "\t- ").TrimEnd());

                if (!ignoreErrors)
                {
                    throw;
                }
            }

            return result;
        }

        internal static async Task<NupkgResult> DownloadNupkgV3Async(CatalogEntry entry, string rootDir, DownloadMode mode, bool ignoreErrors, ILogger log, ILogger deepLog, CancellationToken token)
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
                        log.LogInformation(nupkgFile.FullName);

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
                                reader.ExtractFile(nuspecFile, nuspecPath, deepLog);
                            }

                            // Write package hash
                            File.WriteAllText(hashPath, packageHash);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancelled tasks
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                log.LogWarning($"Unable to download {entry.Id} {entry.Version.ToFullString()} to {nupkgPath}"
                    + Environment.NewLine
                    + MirrorUtility.GetExceptions(ex, "\t- "));
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to download {entry.Id} {entry.Version.ToFullString()} to {nupkgPath}"
                    + Environment.NewLine
                    + MirrorUtility.GetExceptions(ex, "\t- ").TrimEnd());

                if (!ignoreErrors)
                {
                    throw;
                }
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