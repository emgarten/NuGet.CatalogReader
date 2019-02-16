using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CatalogReader
{
    internal static class NuGetExtensions
    {
        internal static Task<JObject> GetJObjectAsync(this HttpSource source, Uri uri, HttpSourceCacheContext cacheContext, ILogger log, CancellationToken token)
        {
            var cacheKey = GetHashKey(uri);

            var request = new HttpSourceCachedRequest(uri.AbsoluteUri, cacheKey, cacheContext)
            {
                EnsureValidContents = stream => CatalogReaderUtility.LoadJson(stream, true),
                IgnoreNotFounds = false
            };

            return source.GetAsync(request, ProcessJson, log, token);
        }

        private static Task<JObject> ProcessJson(HttpSourceResult result)
        {
            return CatalogReaderUtility.LoadJsonAsync(result.Stream, false);
        }

        internal static Task<NuspecReader> GetNuspecAsync(this HttpSource source, Uri uri, HttpSourceCacheContext cacheContext, ILogger log, CancellationToken token)
        {
            var cacheKey = GetHashKey(uri);

            var request = new HttpSourceCachedRequest(uri.AbsoluteUri, cacheKey, cacheContext)
            {
                IgnoreNotFounds = false,
                EnsureValidContents = stream =>
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true))
                    {
                        XDocument.Load(reader);
                    }
                }
            };

            return source.GetAsync(request, ProcessNuspec, log, token);
        }

        private static Task<NuspecReader> ProcessNuspec(HttpSourceResult result)
        {
            return Task.FromResult(new NuspecReader(result.Stream));
        }

        internal static Task<HttpSourceResult> GetNupkgAsync(this HttpSource source, Uri uri, HttpSourceCacheContext cacheContext, ILogger log, CancellationToken token)
        {
            var cacheKey = GetHashKey(uri);

            var request = new HttpSourceCachedRequest(uri.AbsoluteUri, cacheKey, cacheContext)
            {
                IgnoreNotFounds = false,
                EnsureValidContents = stream =>
                {
                    using (var reader = new PackageArchiveReader(stream, leaveStreamOpen: true))
                    {
                        reader.NuspecReader.GetIdentity();
                    }
                }
            };

            return source.GetAsync(request, ProcessResult, log, token);
        }

        private static Task<HttpSourceResult> ProcessResult(HttpSourceResult result)
        {
            return Task.FromResult(result);
        }

        private static string GetHashKey(Uri uri)
        {
            return uri.AbsolutePath.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        }

        private static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl/Versioned", "RegistrationsBaseUrl/3.6.0", "RegistrationsBaseUrl/3.4.0", "RegistrationsBaseUrl/3.0.0-beta" };
        private static readonly string[] PackageBaseAddressUrl = { "PackageBaseAddress/3.0.0" };
        private static readonly string[] CatalogServiceUrl = { "Catalog/3.0.0", "http://schema.emgarten.com/sleet#Catalog/1.0.0" };
        private static readonly string[] SleetPackageIndexUrl = { "http://schema.emgarten.com/sleet#SymbolsPackageIndex/1.0.0" };

        internal static Uri GetSleetPackageIndexUrl(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetServiceUri(SleetPackageIndexUrl);
        }

        internal static Uri GetCatalogServiceUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetServiceUri(CatalogServiceUrl);
        }

        internal static Uri GetPackageBaseAddressUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetServiceUri(PackageBaseAddressUrl);
        }

        internal static Uri GetRegistrationBaseUri(this ServiceIndexResourceV3 serviceIndex)
        {
            return serviceIndex.GetServiceUri(RegistrationsBaseUrl);
        }

        internal static Uri GetServiceUri(this ServiceIndexResourceV3 serviceIndex, string[] types)
        {
            var uris = serviceIndex.GetServiceEntryUris(types);

            if (uris.Count < 1)
            {
                throw new InvalidDataException($"Unable to find a service of type: {string.Join(", ", types)} Verify the index.json file contains this entry.");
            }

            return uris[0];
        }
    }
}
