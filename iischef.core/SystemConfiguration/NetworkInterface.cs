using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iischef.core.SystemConfiguration
{
    /// <summary>
    /// Represents a network interface available in this machine
    /// </summary>
    public class NetworkInterface
    {
        /// <summary>
        /// Identifier/alias
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Ip address
        /// </summary>
        public string ip { get; set; }

        /// <summary>
        /// Force any attempt to bind to this network interface
        /// to be looped through the hosts file.
        /// </summary>
        public bool forcehosts { get; set; }
    }
}
