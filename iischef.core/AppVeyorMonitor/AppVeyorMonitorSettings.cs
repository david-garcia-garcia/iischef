namespace iischef.core.AppVeyorMonitor
{
    /// <summary>
    /// Information about an application to monitor in appveyor
    /// </summary>
    public class AppVeyorMonitorSettings
    {
        /// <summary>
        /// Project name in Appveyor.
        /// </summary>
        public string project { get; set; }

        /// <summary>
        /// Username owner of the API token.
        /// </summary>
        public string username { get; set; }

        /// <summary>
        /// API Token.
        /// </summary>
        public string apitoken { get; set; }
    }
}
