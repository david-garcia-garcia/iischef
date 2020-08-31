using System;

namespace iischef.core.IIS
{
    public class Binding
    {
        /// <summary>
        /// Id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Hostname
        /// </summary>
        public string hostname { get; set; }

        /// <summary>
        /// Port to bind
        /// </summary>
        public int port { get; set; }

        /// <summary>
        /// Network interface
        /// </summary>
        public string @interface { get; set; }

        /// <summary>
        /// Wether or not to add this to the hosts file, for an internal loop.
        /// </summary>
        public bool addtohosts { get; set; }

        /// <summary>
        /// The type of binding, can be HTTP or HTTPS
        /// </summary>
        public string type { get; set; } = "http";

        /// <summary>
        /// The ssl certificate
        /// </summary>
        public string ssl_certificate_friendly_name { get; set; }

        /// <summary>
        /// Automatically deploy a let's encrypt certificate for the binding
        /// </summary>
        public bool ssl_letsencrypt { get; set; }

        /// <summary>
        /// Check if this an ssl binding
        /// </summary>
        /// <returns></returns>
        public bool IsSsl()
        {
            return string.Equals(this.type, "https", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Scheme for the binding
        /// </summary>
        /// <returns></returns>
        public string GetScheme()
        {
            if (this.IsSsl())
            {
                return "https";
            }

            return "http";
        }
    }
}
