namespace iischef.core.Services
{
    public class CouchbaseServiceSettings : DeployerSettingsBase
    {
        /// <summary>
        /// Type of service
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Id for the couchbase service
        /// </summary>
        public string id { get; set; }
    }
}
