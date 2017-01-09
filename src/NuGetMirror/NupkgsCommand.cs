using System;
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
                    var entries = await catalogReader.GetFlattenedEntriesAsync(start, end, token);

                    // Download files
                    var files = await ProcessEntriesUtility.RunAsync<FileInfo>(
                        apply: e => DownloadNupkgAsync(e, root.FullName, mode, ignoreErrors.HasValue(), log, token),
                        maxThreads: maxThreads,
                        token: token,
                        entries: entries);

                    files = files.Where(e => e != null).ToList();

                    if (outputFilesInfo != null)
                    {
                        // Write out new files
                        using (var writer = new StreamWriter(new FileStream(outputFilesInfo.FullName, FileMode.CreateNew)))
                        {
                            foreach (var file in files)
                            {
                                writer.WriteLine(file.FullName);
                            }
                        }
                    }

                    foreach (var file in files)
                    {
                        log.LogMinimal($"new: {file.FullName}");
                    }

                    timer.Stop();

                    var plural = files.Count == 1 ? "" : "s";
                    log.LogMinimal($"Downloaded {files.Count} nupkg{plural} in {timer.Elapsed.ToString()}.");
                }

                return 0;
            });
        }

        internal static Task<FileInfo> DownloadNupkgAsync(CatalogEntry entry, string rootDir, DownloadMode mode, bool ignoreErrors, ILogger log, CancellationToken token)
        {
            // id/version/id.version.nupkg
            var outputDir = Path.Combine(rootDir, entry.Id.ToLowerInvariant(), entry.Version.ToNormalizedString().ToLowerInvariant());

            try
            {
                return entry.DownloadNupkgAsync(outputDir, mode, token);
            }
            catch (Exception ex) when (ignoreErrors)
            {
                MirrorUtility.LogExceptionAsWarning(ex, log);
            }

            return null;
        }
    }
}