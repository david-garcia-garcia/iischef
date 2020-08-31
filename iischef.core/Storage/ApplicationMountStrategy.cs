namespace iischef.core.Storage
{
    public static class ApplicationMountStrategy
    {
        /// <summary>
        /// Full copy from original path
        /// </summary>
        public const string Copy = "copy";

        /// <summary>
        /// Point directly to the original path
        /// </summary>
        public const string Original = "original";

        /// <summary>
        /// Symlink to the original path
        /// </summary>
        public const string Link = "link";

        /// <summary>
        /// Move the original files to the new location
        /// </summary>
        public const string Move = "move";
    }
}
