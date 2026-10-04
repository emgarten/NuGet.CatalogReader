namespace NuGet.CatalogReader
{
    public enum DownloadMode
    {
        /// <summary>
        /// Fail if file exists
        /// </summary>
        FailIfExists = 0,

        /// <summary>
        /// Overwrite if the existing file is older than the entry.
        /// Catalog entries use their commit time, other entries use the current time.
        /// </summary>
        OverwriteIfNewer = 1,

        /// <summary>
        /// Always overwrite
        /// </summary>
        Force = 2,

        /// <summary>
        /// Skip if file exists
        /// </summary>
        SkipIfExists = 3,
    }
}
