using System;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    public static class NuGetv3FeedBuilder
    {
        public static Uri GetPackageRegistrationUri(Uri registrationBaseUri, string id, NuGetVersion version)
        {
            var idFixed = id.ToLowerInvariant();
            var versionFixed = version.ToNormalizedString().ToLowerInvariant();
            var baseUrl = EnsureNoTrailingSlash(registrationBaseUri);

            return new Uri($"{baseUrl}/{idFixed}/{versionFixed}.json");
        }

        public static Uri GetRegistrationIndexUri(Uri registrationBaseUri, string id)
        {
            var idFixed = id.ToLowerInvariant();
            var baseUrl = EnsureNoTrailingSlash(registrationBaseUri);

            return new Uri($"{baseUrl}/{idFixed}/index.json");
        }

        public static Uri GetPackageBaseAddressIndexUri(Uri packageBaseAddress, string id)
        {
            var idFixed = id.ToLowerInvariant();
            var baseUrl = EnsureNoTrailingSlash(packageBaseAddress);

            return new Uri($"{baseUrl}/{idFixed}/index.json");
        }

        public static Uri GetNuspecUri(Uri packageBaseAddress, string id, NuGetVersion version)
        {
            var idFixed = id.ToLowerInvariant();
            var versionFixed = version.ToNormalizedString().ToLowerInvariant();
            var baseUrl = EnsureNoTrailingSlash(packageBaseAddress);

            return new Uri($"{baseUrl}/{idFixed}/{versionFixed}/{idFixed}.nuspec");
        }

        public static Uri GetNupkgUri(Uri packageBaseAddress, string id, NuGetVersion version)
        {
            var idFixed = id.ToLowerInvariant();
            var versionFixed = version.ToNormalizedString().ToLowerInvariant();
            var baseUrl = EnsureNoTrailingSlash(packageBaseAddress);

            return new Uri($"{baseUrl}/{idFixed}/{versionFixed}/{idFixed}.{versionFixed}.nupkg");
        }

        private static string EnsureNoTrailingSlash(Uri uri)
        {
            return uri.AbsoluteUri.TrimEnd('/');
        }
    }
}
