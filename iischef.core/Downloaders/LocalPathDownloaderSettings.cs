using System.Collections.Generic;

namespace iischef.core.Downloaders
{
    /// <summary>
    /// Downlader to be used for projects
    /// that exist in the local filesystem
    /// usually development checkouts.
    /// </summary>
    public class LocalPathDownloaderSettings
    {
        /// <summary>
        /// Type. Must be "localpath"
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// The path to the local artifact.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// By default the system will try to mount
        /// the application directly on top of the specified directory.
        /// This is always not possible/recommended, use a symlink instead
        /// for those cases.
        /// </summary>
        public bool symlink { get; set; }

        /// <summary>
        /// Para saber cuando lanzar un nuevo despliegue, monitorizar cambios
        /// en estos ficheros.
        /// </summary>
        public List<string> monitorChangesTo { get; set; }
    }
}
