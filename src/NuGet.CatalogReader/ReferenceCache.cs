using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// Cache strings, dates, and versions to reduce memory.
    /// </summary>
    internal class ReferenceCache
    {
        private Dictionary<string, string> _stringCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private Dictionary<DateTimeOffset, DateTimeOffset> _dateCache = new Dictionary<DateTimeOffset, DateTimeOffset>();
        private Dictionary<Version, Version> _systemVersionCache = new Dictionary<Version, Version>();

        // Include metadata in the compare.
        // All catalog versions are normalized so the original string is not a concern.
        private Dictionary<NuGetVersion, NuGetVersion> _versionCache = new Dictionary<NuGetVersion, NuGetVersion>(VersionComparer.VersionReleaseMetadata);

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

            if (!_stringCache.TryGetValue(s, out string v))
            {
                _stringCache.Add(s, s);
                v = s;
            }

            return v;
        }

        internal DateTimeOffset GetDate(string s)
        {
            var date = DateTimeOffset.Parse(s);

            if (!_dateCache.TryGetValue(date, out DateTimeOffset v))
            {
                _dateCache.Add(date, date);
                v = date;
            }

            return v;
        }

        internal NuGetVersion GetVersion(string s)
        {
            var version = NuGetVersion.Parse(s);

            if (!_versionCache.TryGetValue(version, out NuGetVersion v))
            {
                var systemVersion = GetSystemVersion(version);

                // Use cached strings for the version parts
                var releaseLabels = version.ReleaseLabels.Select(label => GetString(label));

                // Rebuild the version without the original string value
                version = new NuGetVersion(systemVersion, releaseLabels, GetString(version.Metadata), originalVersion: null);

                _versionCache.Add(version, version);
                v = version;
            }

            return v;
        }

        private Version GetSystemVersion(NuGetVersion nugetVersion)
        {
            var version = new Version(nugetVersion.Major, nugetVersion.Minor, nugetVersion.Patch, nugetVersion.Revision);

            if (!_systemVersionCache.TryGetValue(version, out Version v))
            {
                _systemVersionCache.Add(version, version);
                v = version;
            }

            return v;
        }
    }
}
