namespace iischef.core.Services
{
    public class SQLServiceSettings : DeployerSettingsBase
    {
        /// <summary>
        /// Service type
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Service id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Recovery mode
        /// </summary>
        public string recoveryModel { get; set; }

        /// <summary>
        /// Custom script to run for deployment
        /// </summary>
        public string customScript { get; set; }
    }
}
