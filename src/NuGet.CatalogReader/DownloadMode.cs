namespace NuGet.CatalogReader
{
    public enum DownloadMode
    {
        /// <summary>
        /// Fail if file exists
        /// </summary>
        FailIfExists = 0,

        /// <summary>
        /// Skip if file exists
        /// </summary>
        SkipIfExists = 0,

        /// <summary>
        /// Overwrite if the file is newer
        /// </summary>
        OverwriteIfNewer = 1,

        /// <summary>
        /// Always overwrite
        /// </summary>
        Force = 2
    }
}
