using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CatalogReader
{
    internal static class NuGetExtensions
    {
        internal static async Task<JObject> GetJObjectAsync(this HttpSource source, Uri uri, HttpSourceCacheContext cacheContext, ILogger log, CancellationToken token)
        {
            var cacheKey = GetHashKey(uri);

            var request = new HttpSourceCachedRequest(uri.AbsoluteUri, cacheKey, cacheContext)
            {
                EnsureValidContents = stream => CatalogReaderUtility.LoadJson(stream),
                IgnoreNotFounds = false
            };

            using (var result = await source.GetAsync(request, log, token))
            {
                return CatalogReaderUtility.LoadJson(result.Stream);
            }
        }

        internal static async Task<Stream> GetStreamAsync(this HttpSource source, Uri uri, HttpSourceCacheContext cacheContext, ILogger log, CancellationToken token)
        {
            var cacheKey = GetHashKey(uri);

            var request = new HttpSourceCachedRequest(uri.AbsoluteUri, cacheKey, cacheContext)
            {
                IgnoreNotFounds = false
            };

            var result = await source.GetAsync(request, log, token);

            return result.Stream;
        }

        private static string GetHashKey(Uri uri)
        {
            return uri.AbsolutePath.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        }

        private static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl/3.4.0", "RegistrationsBaseUrl/3.0.0-beta" };
        private static readonly string[] PackageBaseAddressUrl = { "PackageBaseAddress/3.0.0" };
        private static readonly string[] CatalogServiceUrl = { "Catalog/3.0.0" };

        internal static Uri GetCatalogServiceUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetCatalogServiceUri(CatalogServiceUrl);
        }

        internal static Uri GetPackageBaseAddressUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetCatalogServiceUri(PackageBaseAddressUrl);
        }

        internal static Uri GetRegistrationBaseUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetCatalogServiceUri(RegistrationsBaseUrl);
        }

        internal static Uri GetCatalogServiceUri(this ServiceIndexResourceV3 serviceIndex, string[] types)
        {
            var uris = serviceIndex[types];

            if (uris.Count < 1)
            {
                throw new InvalidDataException($"Unable to find a service of type: {string.Join(", ", types)} Verify the index.json file contains this entry.");
            }

            return uris[0];
        }
    }
}
