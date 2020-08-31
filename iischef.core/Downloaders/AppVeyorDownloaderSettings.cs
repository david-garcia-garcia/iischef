namespace iischef.core.Downloaders
{
    /// <summary>
    /// Download artifacts from AppVeyor
    /// </summary>
    public class AppVeyorDownloaderSettings
    {
        /// <summary>
        /// Type. Must be "appveyor".
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Project name in Appveyor.
        /// </summary>
        public string project { get; set; }

        /// <summary>
        /// Username owner of the API token.
        /// </summary>
        public string username { get; set; }

        /// <summary>
        /// API Token.
        /// </summary>
        public string apitoken { get; set; }

        /// <summary>
        /// Branch to pull
        /// </summary>
        public string branch { get; set; }

        /// <summary>
        /// Only commits that match this expresion
        /// </summary>
        public string publish_regex_filter { get; set; }

        /// <summary>
        /// An appveyor release can have multiple artifacts.
        /// 
        /// Use this as a hint to use when more than one artifact
        /// is present in a build.
        /// </summary>
        public string artifact_regex { get; set; }
    }
}
