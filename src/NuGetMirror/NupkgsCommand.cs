using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;

namespace NuGetMirror
{
    internal static class NupkgsCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("nupkgs", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Mirror nupkgs to a folder.";

            var output = cmd.Option("-o|--output", "Output directory for nupkgs.", CommandOptionType.SingleValue);
            var outputFiles = cmd.Option("--file-list", "Output a list of files added.", CommandOptionType.SingleValue);
            var ignoreErrors = cmd.Option("--ignore-errors", "Ignore errors and continue mirroring packages.", CommandOptionType.NoValue);
            var delay = cmd.Option("--delay", "Delay downloading the latest packages to avoid errors. This value is in minutes. Default: 10", CommandOptionType.SingleValue);

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
                    delayTime = TimeSpan.FromMinutes(Int32.Parse(delay.Value()));
                }

                var root = new DirectoryInfo(outputPath);
                root.Create();

                FileInfo outputFilesInfo = null;

                if (outputFiles.HasValue())
                {
                    outputFilesInfo = new FileInfo(outputFiles.Value());
                    FileUtility.Delete(outputFilesInfo.FullName);
                }

                var files = await MirrorUtility.RunAsync(root.FullName, index, ignoreErrors.HasValue(), delayTime, log, CancellationToken.None);

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

                return 0;
            });
        }
    }
}