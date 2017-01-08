using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// Mirror a feed to disk as a folder of nupkgs.
    /// </summary>
    public static class MirrorUtility
    {
        private const string CursorFile = "cursor.json";

        /// <summary>
        /// Mirror packages to a folder.
        /// Use cursor.json
        /// </summary>
        public static async Task<IReadOnlyList<FileInfo>> RunAsync(
            string outputDirectory,
            Uri serviceIndexUri,
            ILogger log,
            CancellationToken token)
        {
            return await RunAsync(outputDirectory, serviceIndexUri, false, TimeSpan.FromMinutes(10), DownloadMode.SkipIfExists, 4, log, token);
        }

        /// <summary>
        /// Mirror packages to a folder.
        /// Use cursor.json
        /// </summary>
        public static async Task<IReadOnlyList<FileInfo>> RunAsync(
            string outputDirectory,
            Uri serviceIndexUri,
            bool ignoreErrors,
            TimeSpan delayTime,
            ILogger log,
            CancellationToken token)
        {
            return await RunAsync(outputDirectory, serviceIndexUri, ignoreErrors, delayTime, DownloadMode.OverwriteIfNewer, 4, log, token);
        }

        /// <summary>
        /// Mirror packages to a folder.
        /// Use cursor.json
        /// </summary>
        public static async Task<IReadOnlyList<FileInfo>> RunAsync(
            string outputDirectory,
            Uri serviceIndexUri,
            bool ignoreErrors,
            TimeSpan delayTime,
            DownloadMode mode,
            int maxConcurrentDownloads,
            ILogger log,
            CancellationToken token)
        {
            var end = DateTimeOffset.UtcNow.Subtract(delayTime);

            var root = new DirectoryInfo(outputDirectory);
            root.Create();

            // Load start if it exists
            var start = LoadCursor(root);

            var files = await RunAsync(root.FullName, serviceIndexUri, ignoreErrors, mode, start, end, maxConcurrentDownloads, log, token);

            // Write out the end of the range processed
            SaveCursor(root, end);

            return files;
        }

        /// <summary>
        /// Mirror packages to a folder.
        /// This does not use cursor.json
        /// </summary>
        public static async Task<IReadOnlyList<FileInfo>> RunAsync(
            string outputDirectory,
            Uri serviceIndexUri,
            bool ignoreErrors,
            DownloadMode mode,
            DateTimeOffset start,
            DateTimeOffset end,
            int maxConcurrentDownloads,
            ILogger log,
            CancellationToken token)
        {
            // CatalogReader
            using (var catalogReader = new CatalogReader(serviceIndexUri, log))
            {
                // Find the most recent entry for each package in the range
                var entries = await catalogReader.GetFlattenedEntriesAsync(start, end, token);

                // Download files
                var files = await ProcessEntriesUtility.RunAsync<FileInfo>(
                    apply: e => DownloadNupkgAsync(e, outputDirectory, mode, ignoreErrors, log, token),
                    maxThreads: maxConcurrentDownloads,
                    token: token,
                    entries: entries);

                return files.Where(e => e != null).ToList();
            }
        }

        private static Task<FileInfo> DownloadNupkgAsync(CatalogEntry entry, string rootDir, DownloadMode mode, bool ignoreErrors, ILogger log, CancellationToken token)
        {
            // id/version/id.version.nupkg
            var outputDir = Path.Combine(rootDir, entry.Id.ToLowerInvariant(), entry.Version.ToNormalizedString().ToLowerInvariant());

            try
            {
                return entry.DownloadNupkgAsync(outputDir, mode, token);
            }
            catch (Exception ex) when (ignoreErrors)
            {
                LogExceptionAsWarning(ex, log);
            }

            return null;
        }

        /// <summary>
        /// cursor.json path
        /// </summary>
        public static FileInfo GetCursorFile(DirectoryInfo root)
        {
            return new FileInfo(Path.Combine(root.FullName, CursorFile));
        }

        /// <summary>
        /// Load cursor.json if it exists.
        /// If it doesn't exist MinTime is returned.
        /// </summary>
        public static DateTimeOffset LoadCursor(DirectoryInfo root)
        {
            var file = GetCursorFile(root);

            if (file.Exists)
            {
                var json = CatalogReaderUtility.LoadJson(file.OpenRead());

                return DateTimeOffset.Parse(json["cursor"].ToObject<string>());
            }

            return DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Write cursor.json to disk.
        /// </summary>
        public static void SaveCursor(DirectoryInfo root, DateTimeOffset time)
        {
            var file = GetCursorFile(root);

            FileUtility.Delete(file.FullName);

            var json = new JObject()
            {
                { "cursor", time.ToString("o") }
            };

            File.WriteAllText(file.FullName, json.ToString());
        }

        private static void LogExceptionAsWarning(Exception ex, ILogger log)
        {
            if (ex is AggregateException ag)
            {
                foreach (var inner in ag.InnerExceptions)
                {
                    LogExceptionAsWarning(inner, log);
                }
            }
            else
            {
                log.LogWarning(ex.Message);
                log.LogDebug(ex.ToString());
            }
        }
    }
}
