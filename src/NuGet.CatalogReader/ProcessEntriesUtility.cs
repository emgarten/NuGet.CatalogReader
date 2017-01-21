using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CatalogReader
{
    public static class ProcessEntriesUtility
    {
        public static Task<IReadOnlyList<FileInfo>> DownloadNuspecsAsync(string outputDirectory, DownloadMode mode, int maxConcurrentDownloads, CancellationToken token, IEnumerable<CatalogEntry> entries)
        {
            var entriesArray = entries.ToArray();

            if (entriesArray.Distinct().Count() != entriesArray.Length)
            {
                throw new InvalidOperationException("Duplicate entries detected. Entries must be unique by id/version.");
            }

            return RunAsync<FileInfo>(
                apply: e => e.DownloadNuspecAsync(outputDirectory, mode, token),
                maxThreads: maxConcurrentDownloads,
                token: token,
                entries: entriesArray);
        }

        public static Task<IReadOnlyList<FileInfo>> DownloadNupkgsAsync(string outputDirectory, DownloadMode mode, int maxConcurrentDownloads, CancellationToken token, IEnumerable<CatalogEntry> entries)
        {
            var entriesArray = entries.ToArray();

            if (entries.Distinct().Count() != entriesArray.Length)
            {
                throw new InvalidOperationException("Duplicate entries detected. Entries must be unique by id/version.");
            }

            return RunAsync<FileInfo>(
                apply: e => e.DownloadNupkgAsync(outputDirectory, mode, token),
                maxThreads: maxConcurrentDownloads,
                token: token,
                entries: entriesArray);
        }

        /// <summary>
        /// Filter entry list to only the latest version of a package.
        /// </summary>
        public static CatalogEntry[] FilterToLatestPerId(bool includePrerelease, IEnumerable<CatalogEntry> entries)
        {
            return entries.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                      .Select(group => group.Where(e => includePrerelease || !e.Version.IsPrerelease)
                                          .OrderByDescending(e => e.Version)
                                          .FirstOrDefault())
                      .ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="apply">Transform or action to apply to each catalog entry.</param>
        /// <param name="maxThreads">Max threads</param>
        public static async Task<IReadOnlyList<T>> RunAsync<T>(Func<CatalogEntry, Task<T>> apply, int maxThreads, CancellationToken token, IEnumerable<CatalogEntry> entries)
        {
            var entriesArray = entries.ToArray();

            maxThreads = Math.Min(1, maxThreads);

            var files = new List<T>(entriesArray.Length);
            var tasks = new List<Task<T>>(maxThreads);

            // Download with throttling
            foreach (var entry in entriesArray)
            {
                if (tasks.Count == maxThreads)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                    files.Add(task.Result);
                }

                tasks.Add(apply(entry));
            }

            // Wait for all downloads
            while (tasks.Count > 0)
            {
                var task = await Task.WhenAny(tasks);
                tasks.Remove(task);
                files.Add(task.Result);
            }

            return files;
        }
    }
}
