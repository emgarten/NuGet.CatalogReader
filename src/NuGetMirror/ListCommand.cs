using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public static void Register(CommandLineApplication cmdApp, HttpSource? httpSource, ILogger log)
        {
            cmdApp.Command("list", cmd =>
            {
                cmd.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw;
                Run(cmd, httpSource, log);
            });
        }

        private static void Run(CommandLineApplication cmd, HttpSource? httpSource, ILogger log)
        {
            cmd.Description = "List packages from a v3 source.";

            var start = cmd.Option("-s|--start", "Beginning of the commit time range.", CommandOptionType.SingleValue);
            var end = cmd.Option("-e|--end", "End of the commit time range.", CommandOptionType.SingleValue);
            var verbose = cmd.Option("-v|--verbose", "Write out additional network call information.", CommandOptionType.NoValue);
            var argRoot = cmd.Argument("[root]", "V3 feed index.json URI", multipleValues: false);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecuteAsync(async (CancellationToken _) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(argRoot.Value))
                    {
                        throw new ArgumentException("Provide the full http url to a v3 nuget feed or a locally configured source name.");
                    }

                    var sources = PackageSourceProvider.LoadPackageSources(
                        Settings.LoadDefaultSettings(Environment.CurrentDirectory));

                    var source = sources.FirstOrDefault(o => o.Name == argRoot.Value);
                    Uri index;
                    var localHttpSource = httpSource;

                    if (source != null)
                    {
                        index = source.SourceUri;
                        localHttpSource = HttpSource.Create(Repository.Factory.GetCoreV3(source));
                    }
                    else
                    {
                        if (!Uri.TryCreate(argRoot.Value, UriKind.Absolute, out index!))
                        {
                            Debug.Assert(sources is ICollection<PackageSource>);
                            var sourceNames = string.Join(", ", sources.Select(o => o.Name));
                            throw new ArgumentException(
                                $"Invalid feed identifier: '{argRoot.Value}'. " +
                                $"Configured sources are: {sourceNames}");
                        }

                        if (!index.AbsolutePath.EndsWith("/index.json", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException(
                                $"Invalid feed url: '{argRoot.Value}'. " +
                                "Provide the full http url to a v3 nuget feed.");
                        }
                    }

                    var startTime = DateTimeOffset.MinValue;
                    var endTime = DateTimeOffset.UtcNow;

                    if (start.HasValue())
                    {
                        startTime = DateTimeOffset.Parse(start.Value()!, CultureInfo.InvariantCulture);
                    }

                    if (end.HasValue())
                    {
                        endTime = DateTimeOffset.Parse(end.Value()!, CultureInfo.InvariantCulture);
                    }

                    if (log is ConsoleLogger consoleLogger)
                    {
                        consoleLogger.VerbosityLevel = verbose.HasValue()
                            ? LogLevel.Information
                            : LogLevel.Minimal;
                    }

                    using var cacheContext = new SourceCacheContext();
                    using var catalogReader = new CatalogReader(
                        index,
                        localHttpSource ?? throw new InvalidOperationException("HttpSource is required"),
                        cacheContext,
                        TimeSpan.Zero,
                        log);

                    var entries = await catalogReader.GetFlattenedEntriesAsync(startTime, endTime, CancellationToken.None);

                    foreach (var entry in entries
                        .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(e => e.Version))
                    {
                        log.LogMinimal($"{entry.Id} {entry.Version.ToFullString()}");
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    ExceptionUtils.LogException(ex, log);
                    return 1;
                }
            });
        }
    }
}
