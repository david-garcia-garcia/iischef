using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.core.IIS
{
    /// <summary>
    /// IIS Settings
    /// </summary>
    public class IISSettings : DeployerSettingsBase
    {
        public string id { get; set; }

        /// <summary>
        /// Mounts for the site
        /// </summary>
        public Dictionary<string, Mount> mounts { get; set; }

        /// <summary>
        /// Bindings for the site
        /// </summary>
        public Dictionary<string, Binding> bindings { get; set; }

        /// <summary>
        /// Bindings for the CDN domains. Can be the same
        /// for multiple applications.
        /// </summary>
        [Obsolete("Will be removed in a future versions. Use the automatically provisioned CDN url for the site.")]
        public Dictionary<string, Binding> cdn_bindings { get; set; }

        /// <summary>
        /// Virtual directories based on CDN rules
        /// </summary>
        [Obsolete("Will be removed in a future versions. Use the automatically provisioned CDN url for the site.")]
        public Dictionary<string, CdnMount> cdn_mounts { get; set; }

        /// <summary>
        /// Application pools
        /// </summary>
        public Dictionary<string, Pool> pools { get; set; }

        /// <summary>
        /// Ip restrictions
        /// </summary>
        public IISSettingsIpRestrictions ipRestrictions { get; set; }

        /// <summary>
        /// Allowed server variables for the URL Rewrite Module
        /// </summary>
        public List<string> allowedServerVariables { get; set; }

        /// <summary>
        /// Make sure we have minimum default data here....
        /// </summary>
        public void InitializeDefaults()
        {
            // We need at least one pool.
            if (this.pools == null || !this.pools.Any())
            {
                this.pools = new Dictionary<string, Pool>();

                this.pools.Add("default", new Pool()
                {
                    id = "default",
                    AutoStart = true,
                    Enable32BitAppOnWin64 = true,
                    ManagedPipelineMode = "Integrated",
                    ManagedRuntimeVersion = string.Empty,
                    StartMode = "AlwaysRunning"
                });
            }

            // We need at least one binding
            if (this.bindings == null || !this.bindings.Any())
            {
                this.bindings = new Dictionary<string, Binding>();

                this.bindings.Add("default", new Binding()
                {
                    id = "default",
                    hostname = "localhost",
                    @interface = "local",
                    port = 80
                });
            }

            // We need at least one mount
            if (this.mounts == null || !this.mounts.Any())
            {
                this.mounts = new Dictionary<string, Mount>();

                this.mounts.Add("root", new Mount()
                {
                    id = "root",
                    path = "/",
                    root = true
                });
            }
        }

        public Mount getRootMount()
        {
            var m = (from p in this.mounts
                     where p.Value.root == true
                     select p).ToList();

            if (m.Count != 1)
            {
                throw new Exception("The application can have exactly one Root Mount, currently: " + m.Count);
            }

            return m.Single().Value;
        }

        public Pool getPoolForBinding(string pool)
        {
            if (string.IsNullOrEmpty(pool))
            {
                return this.pools.First().Value;
            }

            var m = (from p in this.pools
                     where p.Value.id == pool
                     select p).Single();

            return m.Value;
        }
    }
}
