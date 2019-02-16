using System;
using System.Collections.Generic;
using System.IO;
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
    /// NuGet v3 feed reader that does not require the catalog.
    /// </summary>
    public class FeedReader : HttpReaderBase
    {
        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public FeedReader(Uri indexUri)
            : base(indexUri)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public FeedReader(Uri indexUri, TimeSpan cacheTimeout)
            : base(indexUri, cacheTimeout)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public FeedReader(Uri indexUri, ILogger log)
            : base(indexUri, log)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public FeedReader(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, cacheTimeout, log)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public FeedReader(Uri indexUri, HttpMessageHandler messageHandler)
            : base(indexUri, messageHandler)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public FeedReader(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : base(indexUri, messageHandler, log)
        {
        }

        /// <summary>
        /// FeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public FeedReader(Uri indexUri, HttpSource httpSource, SourceCacheContext cacheContext, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, httpSource, cacheContext, cacheTimeout, log)
        {
        }

        /// <summary>
        /// Find all packages with a given id.
        /// </summary>
        /// <param name="id">Package id</param>
        /// <returns>All package version entries</returns>
        public Task<List<PackageEntry>> GetPackagesById(string id)
        {
            return GetPackagesById(id, CancellationToken.None);
        }

        /// <summary>
        /// Find all packages with a given id.
        /// </summary>
        /// <param name="id">Package id</param>
        /// <returns>All package entries</returns>
        public async Task<List<PackageEntry>> GetPackagesById(string id, CancellationToken token)
        {
            await EnsureServiceIndexAsync(_indexUri, token);
            var baseUri = _serviceIndex.GetPackageBaseAddressUri();
            var index = NuGetv3FeedBuilder.GetPackageBaseAddressIndexUri(baseUri, id);
            var json = await GetJson(index, token);
            var versions = ((JArray)json["versions"]).Select(e => NuGetVersion.Parse(e.ToString()));
            return versions.Select(e => GetEntry(id, e)).ToList();
        }

        /// <summary>
        /// Find all packages with a given ids.
        /// </summary>
        /// <param name="ids">Package ids</param>
        /// <returns>All package entries</returns>
        public Task<List<PackageEntry>> GetPackagesById(IEnumerable<string> ids)
        {
            return GetPackagesById(ids, CancellationToken.None);
        }

        /// <summary>
        /// Find all packages with a given ids.
        /// </summary>
        /// <param name="ids">Package ids</param>
        /// <returns>All package entries</returns>
        public async Task<List<PackageEntry>> GetPackagesById(IEnumerable<string> ids, CancellationToken token)
        {
            var tasks = ids.Select(e => new Func<Task<List<PackageEntry>>>(() => GetPackagesById(e, token)));
            var sets = await TaskUtils.RunAsync(tasks);

            return sets.SelectMany(e => e).OrderBy(e => e).ToList();
        }

        /// <summary>
        /// True if the feed contains a catalog.
        /// </summary>
        public Task<bool> HasCatalog()
        {
            return HasCatalog(CancellationToken.None);
        }

        /// <summary>
        /// True if the feed contains a catalog.
        /// </summary>
        public async Task<bool> HasCatalog(CancellationToken token)
        {
            var serviceIndex = await GetServiceIndexAsync(token);
            var hasCatalog = false;

            try
            {
                hasCatalog = serviceIndex.GetCatalogServiceUri() != null;
            }
            catch (InvalidDataException)
            {
                // does not exist
            }

            return hasCatalog;
        }

        /// <summary>
        /// Feed index.json
        /// </summary>
        public Task<ServiceIndexResourceV3> GetServiceIndexAsync()
        {
            return GetServiceIndexAsync(CancellationToken.None);
        }

        /// <summary>
        /// Feed index.json
        /// </summary>
        public async Task<ServiceIndexResourceV3> GetServiceIndexAsync(CancellationToken token)
        {
            await EnsureServiceIndexAsync(_indexUri, token);
            return _serviceIndex;
        }

        protected PackageEntry GetEntry(string id, NuGetVersion version)
        {
            return new PackageEntry(
                id,
                version,
                _serviceIndex,
                GetJson,
                GetNuspec,
                GetNupkg);
        }
    }
}
