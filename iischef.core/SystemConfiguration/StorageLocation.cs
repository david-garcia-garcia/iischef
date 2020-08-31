using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iischef.core.SystemConfiguration
{
    /// <summary>
    /// Defined a local storage
    /// </summary>
    public class StorageLocation
    {
        /// <summary>
        /// A unique identifier for this storage location
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Local path to the storage location.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// Storage type.
        /// </summary>
        public string type { get; set; }
    }
}
