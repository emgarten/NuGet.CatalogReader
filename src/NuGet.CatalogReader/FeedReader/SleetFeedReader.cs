using System;
using System.Collections.Generic;
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
    public class SleetFeedReader : FeedReader
    {
        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public SleetFeedReader(Uri indexUri)
            : base(indexUri)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public SleetFeedReader(Uri indexUri, TimeSpan cacheTimeout)
            : base(indexUri, cacheTimeout)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public SleetFeedReader(Uri indexUri, ILogger log)
            : base(indexUri, log)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        public SleetFeedReader(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, cacheTimeout, log)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public SleetFeedReader(Uri indexUri, HttpMessageHandler messageHandler)
            : base(indexUri, messageHandler)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public SleetFeedReader(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : base(indexUri, messageHandler, log)
        {
        }

        /// <summary>
        /// SleetFeedReader
        /// </summary>
        /// <param name="indexUri">URI of the the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public SleetFeedReader(Uri indexUri, HttpSource httpSource, SourceCacheContext cacheContext, TimeSpan cacheTimeout, ILogger log)
            : base(indexUri, httpSource, cacheContext, cacheTimeout, log)
        {
        }

        /// <summary>
        /// Get all packages from the feed by reading sleet.package.index.json
        /// </summary>
        public Task<List<PackageEntry>> GetPackagesAsync()
        {
            return GetPackagesAsync(CancellationToken.None);
        }

        /// <summary>
        /// Get all packages from the feed by reading sleet.package.index.json
        /// </summary>
        public async Task<List<PackageEntry>> GetPackagesAsync(CancellationToken token)
        {
            var results = new List<PackageEntry>();

            var serviceIndex = await GetServiceIndexAsync(token);
            var packageIndexUri = serviceIndex.GetSleetPackageIndexUrl();
            var json = await GetJson(packageIndexUri, token);
            foreach (var child in ((JObject)json["packages"]).Properties())
            {
                var id = child.Name;
                var versions = (JArray)child.Value;
                results.AddRange(versions.Select(e => GetEntry(id, NuGetVersion.Parse(e.ToString()))));
            }
        
            return results;
        }
    }
}
