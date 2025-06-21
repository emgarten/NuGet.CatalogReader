using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CatalogValidator
{
    /// <summary>
    /// Validate a v3 feed.
    /// </summary>
    internal static class ValidateCommand
    {
        public static void Register(CommandLineApplication cmdApp, HttpSource httpSource, ILogger consoleLog)
        {
            cmdApp.Command("validate", cmd =>
            {
                cmd.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw;
                Run(cmd, httpSource, consoleLog);
            });
        }

        private static void Run(CommandLineApplication cmd, HttpSource httpSource, ILogger consoleLog)
        {
            cmd.Description = "Validate a v3 feed.";

            var delay = cmd.Option("--delay", "Avoid downloading the very latest packages on the feed to avoid errors. This value is in minutes. Default: 10", CommandOptionType.SingleValue);
            var maxThreadsOption = cmd.Option("--max-threads", "Maximum number of concurrent downloads. Default: 8", CommandOptionType.SingleValue);
            var verbose = cmd.Option("--verbose", "Output additional network information.", CommandOptionType.NoValue);

            var argRoot = cmd.Argument(
                "[root]",
                "V3 feed index.json URI",
                multipleValues: false);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecuteAsync(async (CancellationToken _) =>
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

                var delayTime = TimeSpan.FromMinutes(10);

                if (delay.HasValue())
                {
                    if (int.TryParse(delay.Value(), out int x))
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
                    if (int.TryParse(maxThreadsOption.Value(), out int x))
                    {
                        maxThreads = Math.Max(1, x);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid --max-threads value. This must be an integer.");
                    }
                }

                var batchSize = 4096;
                var start = DateTimeOffset.MinValue;
                var end = DateTimeOffset.UtcNow.Subtract(delayTime);
                var token = CancellationToken.None;

                // Loggers
                // source -> deep -> file -> Console
                var log = consoleLog;
                var deepLogger = new FilterLogger(log, LogLevel.Error);

                // Init
                log.LogInformation($"Validating {index.AbsoluteUri}");

                log.LogInformation("Range start:\t" + start.ToString("o"));
                log.LogInformation("Range end:\t" + end.ToString("o"));
                log.LogInformation($"Batch size:\t{batchSize}");
                log.LogInformation($"Threads:\t{maxThreads}");

                var success = true;

                // CatalogReader
                using (var httpClient = new HttpClient())
                using (var cacheContext = new SourceCacheContext())
                using (var catalogReader = new CatalogReader.CatalogReader(index, httpSource, cacheContext, TimeSpan.Zero, deepLogger))
                {
                    // Find the most recent entry for each package in the range
                    // Order by oldest first
                    IEnumerable<CatalogEntry> entryQuery = (await catalogReader
                        .GetFlattenedEntriesAsync(start, end, token));

                    var toProcess = new Queue<CatalogEntry>(entryQuery.OrderBy(e => e.CommitTimeStamp));

                    log.LogInformation($"Catalog entries found: {toProcess.Count}");

                    var complete = 0;
                    var total = toProcess.Count;

                    // Download files
                    var tasks = new List<Task<ValidationResult>>(maxThreads);
                    var results = new List<ValidationResult>();
                    // Download with throttling
                    while (toProcess.Count > 0)
                    {
                        // Create batches
                        var batch = new Queue<CatalogEntry>(batchSize);
                        var batchTimer = new Stopwatch();
                        batchTimer.Start();

                        while (toProcess.Count > 0 && batch.Count < batchSize)
                        {
                            batch.Enqueue(toProcess.Dequeue());
                        }

                        var batchCount = batch.Count;

                        while (batch.Count > 0)
                        {
                            if (tasks.Count == maxThreads)
                            {
                                await CompleteTaskAsync(tasks, results);
                            }

                            var entry = batch.Dequeue();

                            // Run
                            tasks.Add(Task.Run(async () => await VerifyNupkgExistsAsync(entry, httpClient, NullLogger.Instance, token)));
                        }

                        // Wait for all batch downloads
                        while (tasks.Count > 0)
                        {
                            await CompleteTaskAsync(tasks, results);
                        }

                        complete += batchCount;
                        batchTimer.Stop();

                        // Update cursor
                        log.LogMinimal($"================[batch complete]================");
                        log.LogMinimal($"Processed:\t\t{complete} / {total}");
                        log.LogMinimal($"Batch time:\t\t{batchTimer.Elapsed}");

                        var rate = batchTimer.Elapsed.TotalSeconds / Math.Max(1, batchCount);
                        var timeLeft = TimeSpan.FromSeconds(rate * (total - complete));

                        var timeLeftString = string.Empty;

                        if (timeLeft.TotalHours >= 1)
                        {
                            timeLeftString = $"{(int)timeLeft.TotalHours} hours";
                        }
                        else if (timeLeft.TotalMinutes >= 1)
                        {
                            timeLeftString = $"{(int)timeLeft.TotalMinutes} minutes";
                        }
                        else
                        {
                            timeLeftString = $"{(int)timeLeft.TotalSeconds} seconds";
                        }

                        log.LogMinimal($"Estimated time left:\t{timeLeftString}");
                        log.LogMinimal($"================================================");

                        // Free up space
                        catalogReader.ClearCache();
                    }

                    timer.Stop();

                    log.LogMinimal($"Validation time: {timer.Elapsed}");

                    foreach (var group in results.GroupBy(e => e.Type))
                    {
                        log.LogMinimal($"=====[Validation: {group.Key}]=====");

                        foreach (var entry in group.OrderBy(e => e.Entry.Id, StringComparer.OrdinalIgnoreCase))
                        {
                            success = false;
                            log.LogError(entry.Message);
                        }
                    }

                    if (results.Count == 0)
                    {
                        log.LogMinimal("No errors found!");
                    }
                }

                return success ? 0 : 1;
            });
        }

        private static async Task CompleteTaskAsync(List<Task<ValidationResult>> tasks, List<ValidationResult> results)
        {
            var task = await Task.WhenAny(tasks);
            tasks.Remove(task);

            if (!task.Result.Success)
            {
                results.Add(task.Result);
            }
        }

        private static async Task<ValidationResult> VerifyNupkgExistsAsync(CatalogEntry entry, HttpClient httpClient, ILogger log, CancellationToken token)
        {
            var status = await GetStatusCodeAsync(entry.NupkgUri, httpClient, log, token);

            var result = new ValidationResult()
            {
                Entry = entry,
                Success = status == HttpStatusCode.OK,
                Message = status == HttpStatusCode.OK ? string.Empty : $"Error: {entry.NupkgUri.AbsoluteUri} Status code: {status} Catalog entry: {entry.Uri.AbsoluteUri} Id: {entry.Id} Version: {entry.Version.ToNormalizedString()}",
                Type = ValidationType.Nupkg
            };

            return result;
        }

        private static async Task<HttpStatusCode> GetStatusCodeAsync(Uri uri, HttpClient httpClient, ILogger log, CancellationToken token)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Head, uri);

                    var response = await httpClient.SendAsync(request, token);

                    return response.StatusCode;
                }
                catch
                {
                    // Try again.
                    await Task.Delay(100);
                }
            }

            return HttpStatusCode.ServiceUnavailable;
        }

        internal class ValidationResult
        {
            public bool Success { get; set; }

            public ValidationType Type { get; set; }

            public string Message { get; set; }

            public CatalogEntry Entry { get; set; }
        }

        internal enum ValidationType
        {
            Nupkg = 1,
            Nuspec = 2
        }
    }
}