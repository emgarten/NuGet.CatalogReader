using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NuGetMirror;
using McMaster.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    internal static class ListCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger log)
        {
            cmdApp.Command("list", cmd =>
            {
                cmd.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw;
                Run(cmd, httpSource, log);
            });
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

            cmd.OnExecuteAsync(async (CancellationToken _) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(argRoot.Value))
                    {
                        throw new ArgumentException("Provide the full http url to a v3 nuget feed or a locally configured source name.");
                    }

                    // Attempts to load a configured source (including inactive ones) and fallback to Uri parsing if it fails
                    Uri index;
                    var sources = PackageSourceProvider.LoadPackageSources(Settings.LoadDefaultSettings(Environment.CurrentDirectory));
                    var source = sources.FirstOrDefault(o => o.Name == argRoot.Value);
                    if (source != null)
                    {
                        index = source.SourceUri;
                        httpSource = HttpSource.Create(Repository.Factory.GetCoreV3(source));
                    }
                    else
                    {
                        if (!Uri.TryCreate(argRoot.Value, UriKind.Absolute, out index))
                        {
                            // The enumerable is safe to re-iterate because it's a List implementation
                            Debug.Assert(sources is ICollection<PackageSource>, "Sources implementation changed");
                            var sourceNames = string.Join(", ", sources.Select(o => o.Name));
                            throw new ArgumentException($"Invalid feed identifier: '{argRoot.Value}'. Provide the full http url to a v3 nuget feed or a locally configured identifier. Configured sources are: {sourceNames}");
                        }

                        if (!index.AbsolutePath.EndsWith("/index.json", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException($"Invalid feed url: '{argRoot.Value}'. Provide the full http url to a v3 nuget feed.");
                        }
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