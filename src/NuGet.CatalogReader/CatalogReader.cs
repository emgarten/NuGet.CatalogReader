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
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;
        private readonly HttpSourceCacheContext _cacheContext;
        private readonly SourceCacheContext _sourceCacheContext;
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
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, cacheTimeout: TimeSpan.Zero, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, cacheTimeout: cacheTimeout, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, ILogger log)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, cacheTimeout: TimeSpan.Zero, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, cacheTimeout: cacheTimeout, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler)
            : this(indexUri,
                  CatalogReaderUtility.CreateSource(indexUri, messageHandler, new HttpClientHandler()),
                  cacheContext: null,
                  cacheTimeout: TimeSpan.Zero,
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
                  CatalogReaderUtility.CreateSource(indexUri, messageHandler, new HttpClientHandler()),
                  cacheContext: null,
                  cacheTimeout: TimeSpan.Zero,
                  log: log)
        {
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

            if (_httpSource == null)
            {
                _httpSource = CatalogReaderUtility.CreateSource(indexUri);
            }

            if (cacheTimeout == null)
            {
                cacheTimeout = TimeSpan.Zero;
            }

            _cacheContext = new HttpSourceCacheContext(_sourceCacheContext, cacheTimeout);

            if (_sourceCacheContext == null)
            {
                _cacheContext = new HttpSourceCacheContext(new SourceCacheContext(), cacheTimeout);
            }
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
        /// Read all catalog entries.
        /// </summary>
        /// <param name="start">End time of the previous window. Commits exactly matching the start time will NOT be included. This is designed to take the cursor time.</param>
        /// <param name="end">Maximum time to include. Exact matches will be included.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries within the start and end time. Start time is NOT included.</returns>
        public async Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var index = await GetCatalogIndexAsync(token);

            var pages = new List<Tuple<DateTimeOffset, Uri>>();

            if (index["items"] != null)
            {
                foreach (var item in index["items"])
                {
                    var dateTimeString = item.Value<string>("commitTimeStamp");
                    var dateTime = DateTimeOffset.Parse(dateTimeString);
                    var pageUri = new Uri(item["@id"].ToString());

                    pages.Add(new Tuple<DateTimeOffset, Uri>(dateTime, pageUri));
                }
            }
            else
            {
                Debug.Fail("invalid page: " + _indexUri.AbsoluteUri);
            }

            // Take all commits in the range specified
            // Also take the pages before and after, these may include commits within the specified range also.
            var commitsInRange = pages.Where(p => p.Item1 <= end && p.Item1 > start);
            var commitAfter = pages.Where(p => p.Item1 > end).OrderBy(p => p.Item1).FirstOrDefault();
            var commitBefore = pages.Where(p => p.Item1 <= start).OrderByDescending(p => p.Item1).FirstOrDefault();

            var uris = new HashSet<Uri>();

            if (commitAfter != null)
            {
                uris.Add(commitAfter.Item2);
            }

            if (commitBefore != null)
            {
                uris.Add(commitBefore.Item2);
            }

            uris.UnionWith(commitsInRange.Select(p => p.Item2));

            var entries = await GetEntriesAsync(uris, token);

            return entries.Where(e => e.CommitTimeStamp > start && e.CommitTimeStamp <= end).ToList();
        }

        /// <summary>
        /// Clear the HttpCacheFolder cache folder.
        /// Use this to free up space when downloading large numbers of packages.
        /// </summary>
        public void ClearCache()
        {
            if (Directory.Exists(HttpCacheFolder))
            {
                CatalogReaderUtility.DeleteDirectory(HttpCacheFolder);

                Directory.CreateDirectory(HttpCacheFolder);
            }
        }

        private async Task<IReadOnlyList<CatalogEntry>> GetEntriesCommitTimeDescAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var entries = await GetEntriesAsync(start, end, token);

            return entries.OrderByDescending(e => e.CommitTimeStamp).ToList();
        }

        private async Task<List<CatalogEntry>> GetEntriesAsync(IEnumerable<Uri> pageUris, CancellationToken token)
        {
            var maxThreads = Math.Max(1, MaxThreads);
            var cache = new ReferenceCache();

            var entries = new List<CatalogEntry>();
            var tasks = new List<Task<JObject>>(maxThreads);

            foreach (var uri in pageUris)
            {
                token.ThrowIfCancellationRequested();

                while (tasks.Count > maxThreads)
                {
                    entries.AddRange(await CompleteTaskAsync(tasks, cache));
                }

                tasks.Add(_httpSource.GetJObjectAsync(uri, _cacheContext, _log, token));
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
                var index = await _httpSource.GetJObjectAsync(_indexUri, _cacheContext, _log, token);
                var resources = (index["resources"] as JArray);

                if (resources == null)
                {
                    throw new InvalidOperationException($"{uri.AbsoluteUri} does not contain a 'resources' property. Use the root service index.json for the nuget v3 feed.");
                }

                _serviceIndex = new ServiceIndexResourceV3(index, DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Return catalog/index.json
        /// </summary>
        private async Task<JObject> GetCatalogIndexAsync(CancellationToken token)
        {
            await EnsureServiceIndexAsync(_indexUri, token);

            var catalogRootUri = _serviceIndex.GetCatalogServiceUri();

            return await _httpSource.GetJObjectAsync(catalogRootUri, _cacheContext, _log, token);
        }

        public void Dispose()
        {
            _httpSource.Dispose();
            _sourceCacheContext.Dispose();
        }
    }
}
