namespace iischef.core.Services
{
    public class DiskStore
    {
        /// <summary>
        /// Physical storage path. This is a symlink if on same drive.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// Junction path.
        /// </summary>
        public string junction { get; set; }

        /// <summary>
        /// Canonical junction path
        /// </summary>
        public string originalPath { get; set; }

        /// <summary>
        /// Realpath to the junction, if it was nested
        /// inside another junction...
        /// </summary>
        public string junctionRealPath { get; set; }
    }
}
