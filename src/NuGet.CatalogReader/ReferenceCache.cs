using System;
using System.Collections.Concurrent;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// Cache strings, dates, and versions to reduce memory.
    /// </summary>
    internal class ReferenceCache
    {
        private ConcurrentDictionary<string, string> _stringCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private ConcurrentDictionary<DateTimeOffset, DateTimeOffset> _dateCache = new ConcurrentDictionary<DateTimeOffset, DateTimeOffset>();
        private ConcurrentDictionary<Version, Version> _systemVersionCache = new ConcurrentDictionary<Version, Version>();

        // Include metadata in the compare.
        // All catalog versions are normalized so the original string is not a concern.
        private ConcurrentDictionary<NuGetVersion, NuGetVersion> _versionCache = new ConcurrentDictionary<NuGetVersion, NuGetVersion>(VersionComparer.VersionReleaseMetadata);

        internal string GetString(string s)
        {
            if (ReferenceEquals(s, null))
            {
                return null;
            }

            if (s.Length == 0)
            {
                return string.Empty;
            }

            return _stringCache.GetOrAdd(s, s);
        }

        internal DateTimeOffset GetDate(string s)
        {
            var date = DateTimeOffset.Parse(s);

            return _dateCache.GetOrAdd(date, date);
        }

        internal NuGetVersion GetVersion(string s)
        {
            var version = NuGetVersion.Parse(s);
            return _versionCache.GetOrAdd(version, e => CreateVersion(e));
        }

        private NuGetVersion CreateVersion(NuGetVersion version)
        {
            var systemVersion = GetSystemVersion(version);

            // Use cached strings for the version parts
            var releaseLabels = version.ReleaseLabels.Select(label => GetString(label));

            // Rebuild the version without the original string value
            return new NuGetVersion(systemVersion, releaseLabels, GetString(version.Metadata), originalVersion: null);
        }

        private Version GetSystemVersion(NuGetVersion nugetVersion)
        {
            var version = new Version(nugetVersion.Major, nugetVersion.Minor, nugetVersion.Patch, nugetVersion.Revision);

            return _systemVersionCache.GetOrAdd(version, version);
        }
    }
}
