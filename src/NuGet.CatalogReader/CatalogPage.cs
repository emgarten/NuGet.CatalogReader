using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.CatalogReader
{
    public class CatalogPage : IComparable<CatalogPage>, IEquatable<CatalogPage>
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

        internal CatalogPage(
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
            return Equals(obj as CatalogPage);
        }

        public override string ToString()
        {
            return Uri.AbsoluteUri;
        }

        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

        public int CompareTo(CatalogPage other)
        {
            if (other == null)
            {
                return -1;
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        public bool Equals(CatalogPage other)
        {
            return Uri.Equals(other.Uri);
        }
    }
}
