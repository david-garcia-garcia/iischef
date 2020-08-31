namespace iischef.core.Configuration
{
    /// <summary>
    /// Deployment/Maintenance windows for applications
    /// </summary>
    public class DeploymentWindow
    {
        /// <summary>
        /// The time zone for this deployment window
        /// </summary>
        public string timezone { get; set; }

        /// <summary>
        /// The start time
        /// </summary>
        public string start { get; set; }

        /// <summary>
        /// The end time
        /// </summary>
        public string end { get; set; }
    }
}
