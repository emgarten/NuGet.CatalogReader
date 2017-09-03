using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// Catalog page entry.
    /// </summary>
    public class CatalogEntry : IComparable<CatalogEntry>, IEquatable<CatalogEntry>
    {
        private readonly Func<Uri, CancellationToken, Task<JObject>> _getJson;
        private readonly Func<Uri, CancellationToken, Task<NuspecReader>> _getNuspec;
        private readonly Func<Uri, CancellationToken, Task<HttpSourceResult>> _getNupkg;
        private readonly ServiceIndexResourceV3 _serviceIndex;
        private readonly string[] _urlParts;

        internal CatalogEntry(
            string[] urlParts,
            string type,
            string commitId,
            DateTimeOffset commitTs,
            string id,
            NuGetVersion version,
            ServiceIndexResourceV3 serviceIndex,
            Func<Uri, CancellationToken, Task<JObject>> getJson,
            Func<Uri, CancellationToken, Task<NuspecReader>> getNuspec,
            Func<Uri, CancellationToken, Task<HttpSourceResult>> getNupkg)
        {
            _urlParts = urlParts;
            Types = new List<string>() { type };
            CommitId = commitId;
            CommitTimeStamp = commitTs;
            Id = id;
            Version = version;
            _getJson = getJson;
            _serviceIndex = serviceIndex;
            _getNuspec = getNuspec;
            _getNupkg = getNupkg;
        }

        /// <summary>
        /// Catalog page URI.
        /// </summary>
        public Uri Uri
        {
            get
            {
                return new Uri(string.Join("/", _urlParts));
            }
        }

        /// <summary>
        /// Entry RDF types.
        /// </summary>
        public IReadOnlyList<string> Types { get; }

        /// <summary>
        /// True if the entry has type: nuget:PackageDetails
        /// </summary>
        public bool IsAddOrUpdate
        {
            get
            {
                return Types.Contains("nuget:PackageDetails");
            }
        }

        /// <summary>
        /// True if the entry has type: nuget:PackageDelete
        /// </summary>
        public bool IsDelete
        {
            get
            {
                return Types.Contains("nuget:PackageDelete");
            }
        }

        /// <summary>
        /// Package id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Package version.
        /// </summary>
        public NuGetVersion Version { get; }

        /// <summary>
        /// Commit id.
        /// </summary>
        public string CommitId { get; }

        /// <summary>
        /// Commit timestamp.
        /// </summary>
        public DateTimeOffset CommitTimeStamp { get; }

        /// <summary>
        /// Read the Uri into a JObject. This contains all package details.
        /// </summary>
        public Task<JObject> GetPackageDetailsAsync()
        {
            return GetPackageDetailsAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read the Uri into a JObject. This contains all package details.
        /// </summary>
        public Task<JObject> GetPackageDetailsAsync(CancellationToken token)
        {
            return _getJson(Uri, token);
        }

        /// <summary>
        /// Nupkg download Url.
        /// </summary>
        public Uri NupkgUri
        {
            get
            {
                return NuGetv3FeedBuilder.GetNupkgUri(_serviceIndex.GetPackageBaseAddressUri(), Id, Version);
            }
        }

        /// <summary>
        /// Download the nupkg.
        /// </summary>
        public async Task<Stream> GetNupkgAsync()
        {
            return await GetNupkgAsync(CancellationToken.None);
        }

        /// <summary>
        /// Download the nupkg.
        /// </summary>
        public async Task<Stream> GetNupkgAsync(CancellationToken token)
        {
            var result = await _getNupkg(NupkgUri, token);

            if (result.Stream != null)
            {
                // Use the stream if it exists
                return result.Stream;
            }

            // Open the cache file if the stream does not exist.
            return File.OpenRead(result.CacheFile);
        }

        /// <summary>
        /// Download the nupkg to a folder.
        /// </summary>
        public Task<FileInfo> DownloadNupkgAsync(string outputDirectory)
        {
            return DownloadNupkgAsync(outputDirectory, DownloadMode.FailIfExists, CancellationToken.None);
        }

        /// <summary>
        /// Download the nupkg to a folder.
        /// </summary>
        public async Task<FileInfo> DownloadNupkgAsync(string outputDirectory, DownloadMode mode, CancellationToken token)
        {
            using (var stream = await GetNupkgAsync(token))
            {
                var path = new FileInfo(Path.Combine(outputDirectory, $"{FileBaseName}.nupkg"));

                await CatalogReaderUtility.DownloadFileAsync(stream, path, CommitTimeStamp, mode, token);

                return path;
            }
        }

        /// <summary>
        /// Nuspec download url.
        /// </summary>
        public Uri NuspecUri
        {
            get
            {
                return NuGetv3FeedBuilder.GetNuspecUri(_serviceIndex.GetPackageBaseAddressUri(), Id, Version);
            }
        }

        /// <summary>
        /// Nuspec download url.
        /// </summary>
        public Task<NuspecReader> GetNuspecAsync()
        {
            return GetNuspecAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read the nuspec from the feed.
        /// </summary>
        public async Task<NuspecReader> GetNuspecAsync(CancellationToken token)
        {
            return await _getNuspec(NuspecUri, token);
        }

        /// <summary>
        /// Download the nuspec to a directory.
        /// </summary>
        public Task<FileInfo> DownloadNuspecAsync(string outputDirectory)
        {
            return DownloadNuspecAsync(outputDirectory, DownloadMode.FailIfExists, CancellationToken.None);
        }

        /// <summary>
        /// Download the nuspec to a directory.
        /// </summary>
        public async Task<FileInfo> DownloadNuspecAsync(string outputDirectory, DownloadMode mode, CancellationToken token)
        {
            using (var stream = await GetNupkgAsync(token))
            {
                var path = new FileInfo(Path.Combine(outputDirectory, $"{FileBaseName}.nuspec".ToLowerInvariant()));

                await CatalogReaderUtility.DownloadFileAsync(stream, path, CommitTimeStamp, mode, token);

                return path;
            }
        }

        /// <summary>
        /// PackageBaseAddress index.json url.
        /// </summary>
        public Uri PackageBaseAddressIndexUri
        {
            get
            {
                return NuGetv3FeedBuilder.GetPackageBaseAddressIndexUri(_serviceIndex.GetPackageBaseAddressUri(), Id);
            }
        }

        /// <summary>
        /// Read the PackageBaseAddress index.json. This contains a list of all package versions both listed and unlisted.
        /// </summary>
        public Task<JObject> GetPackageBaseAddressIndexUriAsync()
        {
            return GetPackageBaseAddressIndexUriAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read the PackageBaseAddress index.json. This contains a list of all package versions both listed and unlisted for this package id.
        /// </summary>
        public async Task<JObject> GetPackageBaseAddressIndexUriAsync(CancellationToken token)
        {
            return await _getJson(PackageBaseAddressIndexUri, token);
        }

        /// <summary>
        /// Catalog package registrations url for this package id.
        /// </summary>
        public Uri RegistrationIndexUri
        {
            get
            {
                return NuGetv3FeedBuilder.GetRegistrationIndexUri(_serviceIndex.GetRegistrationBaseUri(), Id);
            }
        }

        /// <summary>
        /// Read the package registrations index for this package id.
        /// </summary>
        public async Task<JObject> GetRegistrationIndexUriAsync()
        {
            return await GetRegistrationIndexUriAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read the package registrations index for this package id.
        /// </summary>
        public async Task<JObject> GetRegistrationIndexUriAsync(CancellationToken token)
        {
            return await _getJson(RegistrationIndexUri, token);
        }

        /// <summary>
        /// Registration for this package version. This is a permalink url that points to the registation and catalog page.
        /// </summary>
        public Uri PackageRegistrationUri
        {
            get
            {
                return NuGetv3FeedBuilder.GetPackageRegistrationUri(_serviceIndex.GetRegistrationBaseUri(), Id, Version);
            }
        }

        /// <summary>
        /// Read the registration for this package version. This is a permalink url that points to the registation and catalog page.
        /// </summary>
        public Task<JObject> GetPackageRegistrationUriAsync()
        {
            return GetPackageRegistrationUriAsync(CancellationToken.None);
        }

        /// <summary>
        /// Read the registration for this package version. This is a permalink url that points to the registation and catalog page.
        /// </summary>
        public async Task<JObject> GetPackageRegistrationUriAsync(CancellationToken token)
        {
            return await _getJson(PackageRegistrationUri, token);
        }

        /// <summary>
        /// True if the package is listed in search.
        /// </summary>
        public Task<bool> IsListedAsync()
        {
            return IsListedAsync(CancellationToken.None);
        }

        /// <summary>
        /// True if the package is listed in search.
        /// </summary>
        public async Task<bool> IsListedAsync(CancellationToken token)
        {
            var json = await GetPackageRegistrationUriAsync(token);

            return json.GetJObjectProperty<bool>("listed");
        }

        /// <summary>
        /// Root file name. Example: packagea.1.0.0
        /// </summary>
        public string FileBaseName
        {
            get
            {
                return $"{Id}.{Version.ToNormalizedString()}".ToLowerInvariant();
            }
        }

        /// <summary>
        /// Compare by date.
        /// </summary>
        /// <param name="other">CatalogEntry</param>
        /// <returns>Comparison int</returns>
        public int CompareTo(CatalogEntry other)
        {
            if (other == null)
            {
                return -1;
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        /// <summary>
        /// Compare on id/version.
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(FileBaseName);
        }

        /// <summary>
        /// Compare on id/version.
        /// </summary>
        /// <param name="obj">Other</param>
        /// <returns>True if equal</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as CatalogEntry);
        }

        /// <summary>
        /// Compare on id/version.
        /// </summary>
        /// <param name="other">Other</param>
        /// <returns>True if equal</returns>
        public bool Equals(CatalogEntry other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version);
        }

        /// <summary>
        /// Id Version Date: Date
        /// </summary>
        public override string ToString()
        {
            var op = "unknown";

            if (IsAddOrUpdate)
            {
                op = "add/edit";
            }

            if (IsDelete)
            {
                op = "delete";
            }

            return $"{Id} {Version.ToFullString()} Date: {CommitTimeStamp.UtcDateTime.ToString("O")} Operation: {op}";
        }
    }
}
