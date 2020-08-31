namespace iischef.core.IIS
{
    public class CdnMount
    {
        /// <summary>
        /// Id for the mount
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The matching domain or directory
        /// </summary>
        public string match { get; set; }

        /// <summary>
        /// The cdn will be routed to an application.
        /// 
        /// Ideally this is a local mount in the HOSTS file
        /// to an application in this same IIS server.
        /// </summary>
        public string destination { get; set; }
    }
}
