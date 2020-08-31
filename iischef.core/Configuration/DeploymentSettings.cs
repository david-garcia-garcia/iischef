using System.Collections.Generic;

namespace iischef.core.Configuration
{
    /// <summary>
    /// These are deployment settings to be set on the server itself, not the commited
    /// application configuration
    /// </summary>
    public class DeploymentSettings
    {
        /// <summary>
        /// The deployment windows.
        /// </summary>
        public Dictionary<string, DeploymentWindow> deployment_windows { get; set; }
    }
}
