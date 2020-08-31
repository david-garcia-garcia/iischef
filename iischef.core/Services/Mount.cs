using System.Collections.Generic;

namespace iischef.core.Services
{
    public class Mount
    {
        /// <summary>
        /// The service ID.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Physical path
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// Path in the artifact folder structure
        /// </summary>
        public string mountpath { get; set; }

        /// <summary>
        /// Persist any contents from the artifact
        /// the overlap with the symlink.
        /// 
        /// Careful as this could overwrite any manual
        /// changes done to the persisted files.
        /// </summary>
        public bool persist_on_deploy { get; set; }
    }
}
