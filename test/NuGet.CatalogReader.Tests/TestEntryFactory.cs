using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.CatalogReader.Tests
{
    /// <summary>
    /// Creates entries backed by fake delegates instead of a feed.
    /// </summary>
    internal static class TestEntryFactory
    {
        internal const string BaseUri = "https://localhost:8080/testFeed/";

        internal static ServiceIndexResourceV3 CreateServiceIndex()
        {
            var json = new JObject
            {
                ["version"] = "3.0.0",
                ["resources"] = new JArray
                {
                    CreateResource(BaseUri + "flatcontainer/", "PackageBaseAddress/3.0.0"),
                    CreateResource(BaseUri + "registration/", "RegistrationsBaseUrl/3.4.0"),
                    CreateResource(BaseUri + "catalog/index.json", "Catalog/3.0.0"),
                }
            };

            return new ServiceIndexResourceV3(json, DateTime.UtcNow);
        }

        internal static PackageEntry CreatePackageEntry(
            string id,
            string version,
            Func<Uri, CancellationToken, Task<JObject>> getJson = null)
        {
            return new PackageEntry(
                id,
                NuGetVersion.Parse(version),
                CreateServiceIndex(),
                getJson ?? UnexpectedRequest<JObject>,
                UnexpectedRequest<NuspecReader>,
                UnexpectedRequest<HttpSourceResult>);
        }

        internal static CatalogEntry CreateCatalogEntry(
            string id,
            string version,
            DateTimeOffset commitTimeStamp,
            string type = "nuget:PackageDetails")
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var uri = $"{BaseUri}catalog/data/{id.ToLowerInvariant()}.{normalizedVersion}.json";

            return new CatalogEntry(
                uri.Split('/'),
                type,
                Guid.NewGuid().ToString(),
                commitTimeStamp,
                id,
                NuGetVersion.Parse(version),
                CreateServiceIndex(),
                UnexpectedRequest<JObject>,
                UnexpectedRequest<NuspecReader>,
                UnexpectedRequest<HttpSourceResult>);
        }

        internal static CatalogPageEntry CreateCatalogPageEntry(int page, DateTimeOffset commitTimeStamp)
        {
            return new CatalogPageEntry(
                new Uri($"{BaseUri}catalog/page{page}.json"),
                new[] { "CatalogPage" },
                Guid.NewGuid().ToString(),
                commitTimeStamp);
        }

        private static JObject CreateResource(string id, string type)
        {
            return new JObject
            {
                ["@id"] = id,
                ["@type"] = type,
            };
        }

        private static Task<T> UnexpectedRequest<T>(Uri uri, CancellationToken token)
        {
            throw new InvalidOperationException($"Unexpected request: {uri.AbsoluteUri}");
        }
    }
}
