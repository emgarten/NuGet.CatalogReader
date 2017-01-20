using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// An entry on the catalog index.json page.
    /// </summary>
    public class CatalogPageEntry : IComparable<CatalogPageEntry>, IEquatable<CatalogPageEntry>
    {
        /// <summary>
        /// Commit id.
        /// </summary>
        public string CommitId { get; }

        /// <summary>
        /// Commit timestamp.
        /// </summary>
        public DateTimeOffset CommitTimeStamp { get; }

        /// <summary>
        /// Catalog page URI.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Entry RDF types.
        /// </summary>
        public IReadOnlyList<string> Types { get; }

        internal CatalogPageEntry(
            Uri uri,
            IEnumerable<string> types,
            string commitId,
            DateTimeOffset commitTs)
        {
            Uri = uri;
            Types = types.ToList();
            CommitId = commitId;
            CommitTimeStamp = commitTs;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CatalogPageEntry);
        }

        public override string ToString()
        {
            return Uri.AbsoluteUri;
        }

        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

        public int CompareTo(CatalogPageEntry other)
        {
            if (other == null)
            {
                return -1;
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        public bool Equals(CatalogPageEntry other)
        {
            return Uri.Equals(other.Uri);
        }
    }
}
