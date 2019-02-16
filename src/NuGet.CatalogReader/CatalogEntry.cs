using System;
using System.Collections.Generic;
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
    public class CatalogEntry : PackageEntry, IComparable<CatalogEntry>, IEquatable<CatalogEntry>
    {
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
            : base(id, version, serviceIndex, getJson, getNuspec, getNupkg)
        {
            _urlParts = urlParts;
            Types = new List<string>() { type };
            CommitId = commitId;
            CommitTimeStamp = commitTs;
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
