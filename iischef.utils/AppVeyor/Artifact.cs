using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.utils.AppVeyor
{
    /// <summary>
    /// Describes an artifact as obtained from the appveyor API
    /// </summary>
    public class Artifact
    {
        /// <summary>
        /// The filename
        /// </summary>
        public string fileName { get; set; }

        /// <summary>
        /// The artifact name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// The artifact type or extension
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// The artifact size
        /// </summary>
        public int size { get; set; }
    }
}
