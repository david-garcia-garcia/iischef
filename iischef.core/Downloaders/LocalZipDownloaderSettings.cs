namespace iischef.core.Downloaders
{
    /// <summary>
    /// Downlader to be used for projects
    /// that exist in the local filesystem
    /// usually development checkouts.
    /// </summary>
    public class LocalZipDownloaderSettings
    {
        /// <summary>
        /// Type. Must be "localzip"
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// The path to the local artifact.
        /// </summary>
        public string path { get; set; }
    }
}
