using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
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

        /// <summary>
        /// Max threads. Set to 1 to disable concurrency.
        /// </summary>
        public int MaxThreads { get; set; } = 32;

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        public CatalogReader(Uri indexUri, ILogger log)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri), cacheContext: null, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri, messageHandler, new HttpClientHandler()), cacheContext: null, log: null)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public CatalogReader(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : this(indexUri, CatalogReaderUtility.CreateSource(indexUri, messageHandler, new HttpClientHandler()), cacheContext: null, log: log)
        {
        }

        /// <summary>
        /// CatalogReader
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public CatalogReader(Uri indexUri, HttpSource httpSource, HttpSourceCacheContext cacheContext, ILogger log)
        {
            _indexUri = indexUri ?? throw new ArgumentNullException(nameof(indexUri));
            _httpSource = httpSource ?? throw new ArgumentNullException(nameof(httpSource));
            _log = log ?? NullLogger.Instance;
            _cacheContext = cacheContext;

            if (_cacheContext == null)
            {
                // Disable caching
                _cacheContext = new HttpSourceCacheContext(new SourceCacheContext(), TimeSpan.Zero);
            }
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
            var index = await GetCatalogIndexAsync(_indexUri, token);

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

        private async Task<IReadOnlyList<CatalogEntry>> GetEntriesCommitTimeDescAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var entries = await GetEntriesAsync(start, end, token);

            return entries.OrderByDescending(e => e.CommitTimeStamp).ToList();
        }

        private async Task<List<CatalogEntry>> GetEntriesAsync(IEnumerable<Uri> pageUris, CancellationToken token)
        {
            var maxThreads = Math.Max(1, MaxThreads);

            var entries = new List<CatalogEntry>();
            var tasks = new List<Task<JObject>>(maxThreads);

            foreach (var uri in pageUris)
            {
                token.ThrowIfCancellationRequested();

                while (tasks.Count > maxThreads)
                {
                    entries.AddRange(await CompleteTaskAsync(tasks));
                }

                tasks.Add(_httpSource.GetJObjectAsync(uri, _log, token));
            }

            while (tasks.Count > 0)
            {
                entries.AddRange(await CompleteTaskAsync(tasks));
            }

            return entries;
        }

        private async Task<List<CatalogEntry>> CompleteTaskAsync(List<Task<JObject>> tasks)
        {
            var entries = new List<CatalogEntry>();

            if (tasks.Count > 0)
            {
                var task = await Task.WhenAny(tasks);
                var json = await task;
                tasks.Remove(task);

                entries.AddRange(GetEntriesFromJson(json));
            }

            return entries;
        }

        private List<CatalogEntry> GetEntriesFromJson(JObject json)
        {
            var entries = new List<CatalogEntry>();

            foreach (var item in json["items"])
            {
                var entry = new CatalogEntry(
                        new Uri(item["@id"].ToString()),
                        item["@type"].ToString(),
                        item["commitId"].ToString(),
                        DateTime.Parse(item["commitTimeStamp"].ToString()),
                        item["nuget:id"].ToString(),
                        NuGetVersion.Parse(item["nuget:version"].ToString()),
                        (uri, token) => GetJson(uri, token));

                entries.Add(entry);
            }

            return entries;
        }

        private Task<JObject> GetJson(Uri uri, CancellationToken token)
        {
            return _httpSource.GetJObjectAsync(uri, _cacheContext, _log, token);
        }

        private async Task<JObject> GetCatalogIndexAsync(Uri uri, CancellationToken token)
        {
            var index = await _httpSource.GetJObjectAsync(_indexUri, _log, token);

            var resources = (index["resources"] as JArray);

            if (resources != null)
            {
                // This is the service index page
                var catalogEntry = resources.FirstOrDefault(e => "Catalog/3.0.0".Equals(e["@type"].ToObject<string>(), StringComparison.OrdinalIgnoreCase));

                if (catalogEntry == null)
                {
                    throw new InvalidOperationException($"{uri.AbsoluteUri} does not contain an entry for Catalog/3.0.0.");
                }
                else
                {
                    // Follow the catalog entry
                    index = await _httpSource.GetJObjectAsync(
                        new Uri(catalogEntry["@id"].ToObject<string>()),
                        _log,
                        token);
                }
            }

            var types = index["@type"] as JArray;

            if (types?.Select(e => e.ToObject<string>()).Contains("CatalogRoot", StringComparer.OrdinalIgnoreCase) != true)
            {
                throw new InvalidOperationException($"{uri.AbsoluteUri} does not contain @type CatalogRoot.");
            }

            return index;
        }

        public void Dispose()
        {
            _httpSource.Dispose();
            _sourceCacheContext.Dispose();
        }
    }
}
