using System;
using System.Linq;
using System.Threading;
using Emgarten.Common;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    internal static class ListCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger log)
        {
            cmdApp.Command("list", (cmd) => Run(cmd, httpSource, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, HttpSource httpSource, ILogger log)
        {
            cmd.Description = "List packages from a v3 source.";

            var start = cmd.Option("-s|--start", "Beginning of the commit time range. Packages commited AFTER this time will be included.", CommandOptionType.SingleValue);
            var end = cmd.Option("-e|--end", "End of the commit time range. Packages commited at this time will be included.", CommandOptionType.SingleValue);
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

                    if (start.HasValue())
                    {
                        startTime = DateTimeOffset.Parse(start.Value());
                    }

                    if (end.HasValue())
                    {
                        endTime = DateTimeOffset.Parse(end.Value());
                    }

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
                    ExceptionUtils.LogException(ex, log);
                }

                return 1;
            });
        }
    }
}