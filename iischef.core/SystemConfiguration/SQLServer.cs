namespace iischef.core.SystemConfiguration
{
    /// <summary>
    /// Information to access a local SQL Server for deployments.
    /// </summary>
    public class SQLServer
    {
        /// <summary>
        /// The service ID
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The server's connection string
        /// </summary>
        public string connectionString { get; set; }

        /// <summary>
        /// If set to true and there is a global domain
        /// configured, the application's identity will
        /// be granted native Windows access to the database
        /// </summary>
        public bool useWindowsAuth { get; set; }

        /// <summary>
        /// The application will be granted the exact credentiales available in connectionString, and no application specific credentials will be generated.
        /// </summary>
        public bool passThroughAuth { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string databaseName { get; set; }
    }
}
