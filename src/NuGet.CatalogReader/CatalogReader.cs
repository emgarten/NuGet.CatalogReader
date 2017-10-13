using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// NuGet v3 catalog reader.
    /// </summary>
    public class CatalogReader : IDisposable
    {
        private readonly Uri _indexUri;
        private HttpSource _httpSource;
        private readonly ILogger _log;
        private readonly HttpSourceCacheContext _cacheContext;
        private readonly SourceCacheContext _sourceCacheContext;
        private readonly HttpMessageHandler _messageHandler;
        private ServiceIndexResourceV3 _serviceIndex;

        /// <summary>
        /// Max threads. Set to 1 to disable concurrency.
        /// </summary>
        public int MaxThreads { get; set; } = 16;

        /// <summary>
        /// Http cache location
        /// </summary>
        public string HttpCacheFolder
        {
            get
            {
                return _cacheContext.RootTempFolder;
            }
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: TimeSpan.Zero, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: cacheTimeout, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, ILogger log)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: TimeSpan.Zero, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: cacheTimeout, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler)
            : this(indexUri,
                  messageHandler: null,
                  log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : this(indexUri,
                  httpSource: null,
                  cacheContext: null,
                  cacheTimeout: TimeSpan.Zero,
                  log: log)
        {
            _messageHandler = messageHandler;
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public CatalogReader(Uri indexUri, HttpSource httpSource, SourceCacheContext cacheContext, TimeSpan cacheTimeout, ILogger log)
        {
            _indexUri = indexUri ?? throw new ArgumentNullException(nameof(indexUri));
            _log = log ?? NullLogger.Instance;
            _sourceCacheContext = cacheContext ?? new SourceCacheContext();

            _httpSource = httpSource;

            // TODO: what should retry be?
            _cacheContext = HttpSourceCacheContext.Create(_sourceCacheContext, 5);

            if (_sourceCacheContext == null)
            {
                var sourceCacheContext = new SourceCacheContext()
                {
                    MaxAge = DateTimeOffset.UtcNow.Subtract(cacheTimeout),
                };

                _cacheContext = HttpSourceCacheContext.Create(sourceCacheContext, 5);
            }
        }

        /// <summary>
        /// Retrieve the HttpSource used for Http requests.
        /// </summary>
        /// <returns></returns>
        public async Task<HttpSource> GetHttpSourceAsync()
        {
            await EnsureHttpSourceAsync();

            return _httpSource;
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
             * Suppose we want to make a query that returns 3 through 6. This API to fetch entries treat the lower
             * timebound as exclusive and the upper time bound as inclusive. Therefore, our query can be summarized as:
             *
             *   (2, 6]
             *
             * Each page is identified in the catalog index by the latest commit timestamp. That is, the commit
             * timestamp of the last commit made to that page. In our example above, we need to fetch pages 
             * P1, P2 and P3. 2 (our lower bound) is greater than P0's commit timestamp 1 so we can eliminate that page
             * and lower. 6 is less than or equal to P3's commit timestamp of 7 so we take up to that page and
             * eliminate P4 and higher. In this example, both pages will include some data we do not care about. P1
             * includes 2 and P3 includes 7. If the upper exactly matches a commit timestamp of a page, we still fetch
             * the next page since it's theoretically possible for two commits to have the same timestamp.
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

            var entries = await GetEntriesAsync(pages, token);

            return entries.Where(e => e.CommitTimeStamp > start && e.CommitTimeStamp <= end).ToList();
        }

        /// <summary>
        /// Clear the HttpCacheFolder cache folder.
        /// Use this to free up space when downloading large numbers of packages.
        /// </summary>
        public void ClearCache()
        {
            CatalogReaderUtility.DeleteDirectoryFiles(HttpCacheFolder);
        }

        private async Task<IReadOnlyList<CatalogEntry>> GetEntriesCommitTimeDescAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var entries = await GetEntriesAsync(start, end, token);

            return entries.OrderByDescending(e => e.CommitTimeStamp).ToList();
        }

        /// <summary>
        /// Retrieve entries for the given index page entries.
        /// </summary>
        public async Task<List<CatalogEntry>> GetEntriesAsync(IEnumerable<CatalogPageEntry> pages, CancellationToken token)
        {
            var maxThreads = Math.Max(1, MaxThreads);
            var cache = new ReferenceCache();

            var entries = new List<CatalogEntry>();
            var tasks = new List<Task<JObject>>(maxThreads);

            foreach (var page in pages)
            {
                token.ThrowIfCancellationRequested();

                while (tasks.Count > maxThreads)
                {
                    entries.AddRange(await CompleteTaskAsync(tasks, cache));
                }

                tasks.Add(_httpSource.GetJObjectAsync(page.Uri, _cacheContext, _log, token));
            }

            while (tasks.Count > 0)
            {
                entries.AddRange(await CompleteTaskAsync(tasks, cache));
            }

            return entries;
        }

        private async Task<List<CatalogEntry>> CompleteTaskAsync(List<Task<JObject>> tasks, ReferenceCache cache)
        {
            var entries = new List<CatalogEntry>();

            if (tasks.Count > 0)
            {
                var task = await Task.WhenAny(tasks);
                var json = await task;
                tasks.Remove(task);

                entries.AddRange(GetEntriesFromJson(json, cache));
            }

            return entries;
        }

        private List<CatalogEntry> GetEntriesFromJson(JObject json, ReferenceCache cache)
        {
            var entries = new List<CatalogEntry>();

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

            return entries;
        }

        private Func<Uri, CancellationToken, Task<JObject>> _getJson;
        private Func<Uri, CancellationToken, Task<JObject>> GetJson
        {
            get
            {
                if (_getJson == null)
                {
                    _getJson = (uri, token) => _httpSource.GetJObjectAsync(uri, _cacheContext, _log, token);
                }

                return _getJson;
            }
        }

        private Func<Uri, CancellationToken, Task<HttpSourceResult>> _getNupkg;
        private Func<Uri, CancellationToken, Task<HttpSourceResult>> GetNupkg
        {
            get
            {
                if (_getNupkg == null)
                {
                    _getNupkg = (uri, token) => _httpSource.GetNupkgAsync(uri, _cacheContext, _log, token);
                }

                return _getNupkg;
            }
        }

        private Func<Uri, CancellationToken, Task<NuspecReader>> _getNuspec;
        private Func<Uri, CancellationToken, Task<NuspecReader>> GetNuspec
        {
            get
            {
                if (_getNuspec == null)
                {
                    _getNuspec = (uri, token) => _httpSource.GetNuspecAsync(uri, _cacheContext, _log, token);
                }

                return _getNuspec;
            }
        }

        /// <summary>
        /// Ensure index.json has been loaded.
        /// </summary>
        private async Task EnsureServiceIndexAsync(Uri uri, CancellationToken token)
        {
            if (_serviceIndex == null)
            {
                await EnsureHttpSourceAsync();

                var index = await _httpSource.GetJObjectAsync(_indexUri, _cacheContext, _log, token);
                var resources = (index["resources"] as JArray);

                if (resources == null)
                {
                    throw new InvalidOperationException($"{uri.AbsoluteUri} does not contain a 'resources' property. Use the root service index.json for the nuget v3 feed.");
                }

                _serviceIndex = new ServiceIndexResourceV3(index, DateTime.UtcNow);
            }
        }

        private async Task EnsureHttpSourceAsync()
        {
            if (_httpSource == null)
            {
                var handlerResource = await CatalogReaderUtility.GetHandlerAsync(_indexUri, _messageHandler);

                var packageSource = new PackageSource(_indexUri.AbsoluteUri);

                _httpSource = new HttpSource(
                    packageSource,
                    () => Task.FromResult((HttpHandlerResource)handlerResource),
                    NullThrottle.Instance);

                if (string.IsNullOrEmpty(UserAgent.UserAgentString) 
                    || new UserAgentStringBuilder().Build()
                        .Equals(UserAgent.UserAgentString, StringComparison.Ordinal))
                {
                    // Set the user agent string if it was not already set.
                    var userAgent = new UserAgentStringBuilder("NuGet.CatalogReader");
                    UserAgent.SetUserAgentString(userAgent);
                }
            }
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

        public void Dispose()
        {
            _httpSource.Dispose();
            _sourceCacheContext.Dispose();
        }
    }
}
