using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetMirror
{
    internal static class CatalogCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger log)
        {
            cmdApp.Command("catalog", (cmd) => Run(cmd, httpSource, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, HttpSource httpSource, ILogger log)
        {
            cmd.Description = "Mirror catalog json files.";

            var output = cmd.Option("-o|--output", "Output directory for nupkgs.", CommandOptionType.SingleValue);
            var verbose = cmd.Option("-v|--verbose", "Write out additional network call information.", CommandOptionType.NoValue);

            var argRoot = cmd.Argument(
                "[root]",
                "V3 feed index.json URI",
                multipleValues: false);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(async () =>
            {
                try
                {
                    if (string.IsNullOrEmpty(argRoot.Value))
                    {
                        throw new ArgumentException("Provide the full http url to a v3 nuget feed.");
                    }

                    var index = new Uri(argRoot.Value);

                    if (!index.AbsolutePath.EndsWith("/index.json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"Invalid feed url: '{argRoot.Value}'. Provide the full http url to a v3 nuget feed.");
                    }

                    var startTime = DateTimeOffset.MinValue;
                    var endTime = DateTimeOffset.UtcNow;

                    if (log is ConsoleLogger consoleLogger)
                    {
                        if (verbose.HasValue())
                        {
                            consoleLogger.VerbosityLevel = LogLevel.Information;
                        }
                        else
                        {
                            consoleLogger.VerbosityLevel = LogLevel.Minimal;
                        }
                    }

                    // CatalogReader
                    using (var cacheContext = new SourceCacheContext())
                    using (var catalogReader = new CatalogReader(index, httpSource, cacheContext, TimeSpan.Zero, log))
                    {
                        if (httpSource == null)
                        {
                            httpSource = await catalogReader.GetHttpSourceAsync();
                        }

                        var indexUrl = await catalogReader.GetCatalogIndexUriAsync(CancellationToken.None);


                        var entries = await catalogReader.GetFlattenedEntriesAsync(startTime, endTime, CancellationToken.None);

                        foreach (var entry in entries
                            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(e => e.Version))
                        {
                            log.LogMinimal($"{entry.Id} {entry.Version.ToFullString()}");
                        }
                    };

                    return 0;
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    log.LogDebug(ex.ToString());
                }

                return 1;
            });
        }
    }
}