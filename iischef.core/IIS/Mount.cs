namespace iischef.core.IIS
{
    public class Mount
    {
        /// <summary>
        /// Alias
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Relative path from artifact or expanded path
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// Only valid for NON root mounts. Tells the name
        /// of the virtual directory.
        /// </summary>
        public string mountpath { get; set; }

        /// <summary>
        /// If this is the root mount
        /// </summary>
        public bool root { get; set; }

        /// <summary>
        /// The pool to use. Use empty for default.
        /// </summary>
        public string pool { get; set; }

        /// <summary>
        /// Preload the application. For faster first use
        /// </summary>
        public bool preloadEnabled { get; set; }

        /// <summary>
        /// Mounts are added by default as applications, use çisVirtualDirectory = true to add them as virtual directories
        /// </summary>
        public bool isVirtualDirectory { get; set; }

        /// <summary>
        /// A machine key will be setup for the mount if
        /// this has been specified. Not valid for virtual directories, and the site
        /// must already a have a valid web.config file.
        /// </summary>
        public string machineKeyDeriveFrom { get; set; }
    }
}
