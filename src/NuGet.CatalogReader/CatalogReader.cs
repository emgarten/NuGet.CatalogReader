using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// NuGet v3 catalog reader.
    /// </summary>
    public class CatalogReader : HttpReaderBase
    {
        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public CatalogReader(Uri indexUri)
            : base(indexUri)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout)
            : base(indexUri, cacheTimeout)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public CatalogReader(Uri indexUri, ILogger log)
            : base(indexUri, log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, cacheTimeout, log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler)
            : base(indexUri, messageHandler)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : base(indexUri, messageHandler, log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public CatalogReader(Uri indexUri, HttpSource httpSource, SourceCacheContext cacheContext, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, httpSource, cacheContext, cacheTimeout, log)
        {
        }

        /// <summary>
        /// Returns only the latest id/version combination for each package.
        /// Older edits and deleted packages are ignored.
        /// </summary>
        public Task<IReadOnlyList<CatalogEntry>> GetFlattenedEntriesAsync()
        {
            return GetFlattenedEntriesAsync(CancellationToken.None);
        }

        /// <summary>
        /// Returns only the latest id/version combination for each package.
        /// Older edits and deleted packages are ignored.
        /// </summary>
        public Task<IReadOnlyList<CatalogEntry>> GetFlattenedEntriesAsync(CancellationToken token)
        {
            return GetFlattenedEntriesAsync(DateTimeOffset.MinValue, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Returns only the latest id/version combination for each package. Older edits are ignored.
        /// </summary>
        /// <param name="start">End time of the previous window. Commits exactly matching the start time will NOT be included. This is designed to take the cursor time.</param>
        /// <param name="end">Maximum time to include. Exact matches will be included.</param>
        /// <returns>Entries within the start and end time. Start time is NOT included.</returns>
        public Task<IReadOnlyList<CatalogEntry>> GetFlattenedEntriesAsync(DateTimeOffset start, DateTimeOffset end)
        {
            return GetFlattenedEntriesAsync(start, end, CancellationToken.None);
        }

        /// <summary>
        /// Returns only the latest id/version combination for each package. Older edits are ignored.
        /// </summary>
        /// <param name="start">End time of the previous window. Commits exactly matching the start time will NOT be included. This is designed to take the cursor time.</param>
        /// <param name="end">Maximum time to include. Exact matches will be included.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries within the start and end time. Start time is NOT included.</returns>
        public async Task<IReadOnlyList<CatalogEntry>> GetFlattenedEntriesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var set = new HashSet<CatalogEntry>();
            var deleted = new HashSet<CatalogEntry>();

            var entries = await GetEntriesCommitTimeDescAsync(start, end, token);

            foreach (var entry in entries)
            {
                if (entry.IsDelete)
                {
                    // Mark as deleted, this has no
                    // impact if the package was re-added. It will 
                    // already be in the set in that case.
                    deleted.Add(entry);
                }
                else if (entry.IsAddOrUpdate && !deleted.Contains(entry))
                {
                    // ignore items we have already seen
                    set.Add(entry);
                }
            }

            return set.OrderByDescending(e => e.CommitTimeStamp).ToList();
        }

        /// <summary>
        /// Returns an index of all non-deleted packages in the catalog.
        /// Id -> Version set.
        /// </summary>
        public Task<IDictionary<string, ISet<NuGetVersion>>> GetPackageSetAsync()
        {
            return GetPackageSetAsync(CancellationToken.None);
        }

        /// <summary>
        /// Returns an index of all non-deleted packages in the catalog.
        /// Id -> Version set.
        /// </summary>
        public Task<IDictionary<string, ISet<NuGetVersion>>> GetPackageSetAsync(CancellationToken token)
        {
            return GetPackageSetAsync(DateTimeOffset.MinValue, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Returns an index of all non-deleted packages in the catalog.
        /// Id -> Version set.
        /// </summary>
        /// <param name="start">End time of the previous window. Commits exactly matching the start time will NOT be included. This is designed to take the cursor time.</param>
        /// <param name="end">Maximum time to include. Exact matches will be included.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries within the start and end time. Start time is NOT included.</returns>
        public async Task<IDictionary<string, ISet<NuGetVersion>>> GetPackageSetAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var packages = new Dictionary<string, ISet<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in await GetFlattenedEntriesAsync(start, end, token))
            {
                if (!packages.TryGetValue(entry.Id, out var versions))
                {
                    versions = new SortedSet<NuGetVersion>();
                    packages.Add(entry.Id, versions);
                }

                versions.Add(entry.Version);
            }

            return packages;
        }

        /// <summary>
        /// Read all catalog entries.
        /// </summary>
        public Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync()
        {
            return GetEntriesAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read all catalog entries.
        /// </summary>
        public Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync(CancellationToken token)
        {
            return GetEntriesAsync(DateTimeOffset.MinValue, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Get catalog pages.
        /// </summary>
        public Task<IReadOnlyList<CatalogPageEntry>> GetPageEntriesAsync(CancellationToken token)
        {
            return GetPageEntriesAsync(DateTimeOffset.MinValue, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Get catalog pages.
        /// </summary>
        public async Task<IReadOnlyList<CatalogPageEntry>> GetPageEntriesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var index = await GetCatalogIndexAsync(token);

            var pages = new List<CatalogPageEntry>();

            if (index["items"] != null)
            {
                foreach (var item in index["items"])
                {
                    var dateTimeString = item.Value<string>("commitTimeStamp");
                    var dateTime = DateTimeOffset.Parse(dateTimeString);
                    var pageUri = new Uri(item["@id"].ToString());
                    var types = new string[] { item.Value<string>("@type") };
                    var commitId = item.Value<string>("commitId");

                    pages.Add(new CatalogPageEntry(pageUri, types, commitId, dateTime));
                }
            }
            else
            {
                Debug.Fail("invalid page: " + _indexUri.AbsoluteUri);
            }

            /*
             * Suppose the following numbers represent the commit timestamps of ten packages published.
             * Also suppose that each timestamp is distinct, for simplicity. Finally, assume page size in the
             * catalog is two. The resulting five pages (identified by P0 .. P4) are:
             * 
             *   0  1  2  3  4  5  6  7  8  9
             *   \__/  \__/  \__/  \__/  \__/
             *    P0    P1    P2    P3    P4
             *
             * Suppose we want to make a query that returns 3 through 6. This API to fetch entries treats the lower
             * time bound as exclusive and the upper time bound as inclusive. Our query can be summarized as:
             *
             *   (2, 6]
             *
             * Each page in the catalog index has an associated latest commit timestamp. That is, the timestamp of the
             * last commit made to that page. In our example above, we need to fetch pages P1, P2 and P3. 2 (our lower
             * bound) is greater than P0's commit timestamp 1 so we can eliminate that page and lower. 6 is less than
             * or equal to P3's commit timestamp of 7 so we take up to that page and eliminate P4 and higher. In this
             * example, both pages will include some data we do not care about. P1 includes 2 and P3 includes 7.
             *
             * Note that if the upper exactly matches a commit timestamp of a page, we still fetch the next page since
             * it's theoretically possible for two commits to have the same timestamp.
             */

            var commitsInRange = pages.Where(p => p.CommitTimeStamp > start && p.CommitTimeStamp <= end);
            var commitAfter = pages.Where(p => p.CommitTimeStamp > end).OrderBy(p => p.CommitTimeStamp).FirstOrDefault();

            var inRange = new HashSet<CatalogPageEntry>(commitsInRange);

            if (commitAfter != null)
            {
                inRange.Add(commitAfter);
            }

            return inRange.OrderBy(e => e.CommitTimeStamp).ToList();
        }

        /// <summary>
        /// Read all catalog entries.
        /// </summary>
        /// <param name="start">End time of the previous window. Commits exactly matching the start time will NOT be included. This is designed to take the cursor time.</param>
        /// <param name="end">Maximum time to include. Exact matches will be included.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries within the start and end time. Start time is NOT included.</returns>
        public async Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var pages = await GetPageEntriesAsync(start, end, token);

            return await GetEntriesAsync(pages, start, end, token);
        }

        private async Task<IReadOnlyList<CatalogEntry>> GetEntriesCommitTimeDescAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var entries = await GetEntriesAsync(start, end, token);

            return entries.OrderByDescending(e => e.CommitTimeStamp).ToList();
        }

        public async Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync(IEnumerable<CatalogPageEntry> pages, DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var entries = await GetEntriesAsync(pages, token);

            return entries.Where(e => e.CommitTimeStamp > start && e.CommitTimeStamp <= end).ToList();
        }

        /// <summary>
        /// Retrieve entries for the given index page entries.
        /// </summary>
        public async Task<List<CatalogEntry>> GetEntriesAsync(IEnumerable<CatalogPageEntry> pages, CancellationToken token)
        {
            var tasks = pages.Select(page =>
                new Func<Task<JObject>>(() =>
                    _httpSource.GetJObjectAsync(page.Uri, _cacheContext, _log, token)));

            var maxThreads = Math.Max(1, MaxThreads);
            var cache = new ReferenceCache();
            var entries = new ConcurrentBag<CatalogEntry>();

            var process = new Func<Task<JObject>, Task<bool>>(e => CompleteTaskAsync(e, cache, entries));

            await TaskUtils.RunAsync(tasks,
                useTaskRun: false,
                maxThreads: maxThreads,
                process: process,
                token: token);

            return entries.ToList();
        }

        private async Task<bool> CompleteTaskAsync(Task<JObject> task, ReferenceCache cache, ConcurrentBag<CatalogEntry> entries)
        {
            var json = await task;

            foreach (var item in json["items"])
            {
                // Store the url in pieces so it can be cached.
                // Split on /
                var urlParts = item["@id"]
                    .ToObject<string>().Split('/')
                    .Select(s => cache.GetString(s))
                    .ToArray();

                var entry = new CatalogEntry(
                        urlParts,
                        cache.GetString(item["@type"].ToObject<string>()),
                        cache.GetString(item["commitId"].ToObject<string>()),
                        cache.GetDate(item["commitTimeStamp"].ToObject<string>()),
                        cache.GetString(item["nuget:id"].ToObject<string>()),
                        cache.GetVersion(item["nuget:version"].ToObject<string>()),
                        _serviceIndex,
                        GetJson,
                        GetNuspec,
                        GetNupkg);

                entries.Add(entry);
            }

            return true;
        }

        /// <summary>
        /// Return the catalog index.json Uri.
        /// </summary>
        public async Task<Uri> GetCatalogIndexUriAsync(CancellationToken token)
        {
            await EnsureServiceIndexAsync(_indexUri, token);

            return _serviceIndex.GetCatalogServiceUri();
        }

        /// <summary>
        /// Return catalog/index.json
        /// </summary>
        private async Task<JObject> GetCatalogIndexAsync(CancellationToken token)
        {
            var catalogRootUri = await GetCatalogIndexUriAsync(token);

            return await _httpSource.GetJObjectAsync(catalogRootUri, _cacheContext, _log, token);
        }
    }
}
