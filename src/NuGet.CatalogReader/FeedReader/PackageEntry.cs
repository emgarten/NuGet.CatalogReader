using System;
using System.IO;
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
    public class PackageEntry : IComparable<PackageEntry>, IEquatable<PackageEntry>
    {
        protected readonly Func<Uri, CancellationToken, Task<JObject>> _getJson;
        protected readonly Func<Uri, CancellationToken, Task<NuspecReader>> _getNuspec;
        protected readonly Func<Uri, CancellationToken, Task<HttpSourceResult>> _getNupkg;
        protected readonly ServiceIndexResourceV3 _serviceIndex;

        internal PackageEntry(
            string id,
            NuGetVersion version,
            ServiceIndexResourceV3 serviceIndex,
            Func<Uri, CancellationToken, Task<JObject>> getJson,
            Func<Uri, CancellationToken, Task<NuspecReader>> getNuspec,
            Func<Uri, CancellationToken, Task<HttpSourceResult>> getNupkg)
        {
            Id = id;
            Version = version;
            _getJson = getJson;
            _serviceIndex = serviceIndex;
            _getNuspec = getNuspec;
            _getNupkg = getNupkg;
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
        public Task<FileInfo> DownloadNupkgAsync(string outputDirectory, DownloadMode mode, CancellationToken token)
        {
            return DownloadNupkgAsync(outputDirectory, mode, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Download the nupkg to a folder.
        /// </summary>
        protected async Task<FileInfo> DownloadNupkgAsync(string outputDirectory, DownloadMode mode, DateTimeOffset date, CancellationToken token)
        {
            using (var stream = await GetNupkgAsync(token))
            {
                var path = new FileInfo(Path.Combine(outputDirectory, $"{FileBaseName}.nupkg"));
                await CatalogReaderUtility.DownloadFileAsync(stream, path, date, mode, token);

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
        public Task<FileInfo> DownloadNuspecAsync(string outputDirectory, DownloadMode mode, CancellationToken token)
        {
            return DownloadNuspecAsync(outputDirectory, mode, DateTimeOffset.UtcNow, token);
        }

        /// <summary>
        /// Download the nuspec to a directory.
        /// </summary>
        protected async Task<FileInfo> DownloadNuspecAsync(string outputDirectory, DownloadMode mode, DateTimeOffset date, CancellationToken token)
        {
            var reader = await GetNuspecAsync(token);

            if (reader == null)
            {
                throw new InvalidOperationException($"Nuspec not found: {NuspecUri}");
            }

            var path = new FileInfo(Path.Combine(outputDirectory, $"{FileBaseName}.nuspec".ToLowerInvariant()));

            File.WriteAllText(path.FullName, reader.Xml.ToString());

            return path;
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
        public int CompareTo(PackageEntry other)
        {
            if (other == null)
            {
                return -1;
            }

            var result = StringComparer.OrdinalIgnoreCase.Compare(Id, other.Id);

            if (result != 0)
            {
                return result;
            }

            return VersionComparer.Default.Compare(Version, other.Version);
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
            return Equals(obj as PackageEntry);
        }

        /// <summary>
        /// Compare on id/version.
        /// </summary>
        /// <param name="other">Other</param>
        /// <returns>True if equal</returns>
        public bool Equals(PackageEntry other)
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
            return $"{Id} {Version.ToFullString()}";
        }
    }
}
