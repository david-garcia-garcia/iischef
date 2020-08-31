using System.Collections.Generic;

namespace iischef.core.IIS
{
    /// <summary>
    /// 
    /// </summary>
    public class IISSettingsIpRestrictions
    {
        /// <summary>
        /// Full enable or disable IP restrictions
        /// </summary>
        public bool enabled { get; set; }

        /// <summary>
        /// Maximum concurrent requests per IP
        /// </summary>
        public bool denyByConcurrentRequests_enabled { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int denyByConcurrentRequests_maxConcurrentRequests { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool denyByRequestRate_enabled { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int denyByRequestRate_maxRequests { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int denyByRequestRate_requestIntervalInMilliseconds { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool enableLoggingOnlyMode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool enableProxyMode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string accessForUnspecifiedClients { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool enableDomainNameRestrictions { get; set; }

        /// <summary>
        /// Deny action type
        /// </summary>
        public string denyAction { get; set; }

        /// <summary>
        ///   
        /// </summary>
        public List<IpEntry> ipSecurity_addresses { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ipSecurity_denyAction { get; set; }

        public string ipSecurity_enableProxyMode { get; set; }

        public string ipSecurity_enableReverseDns { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public class IpEntry
        {
            public string ipAddress { get; set; }

            public string domainName { get; set; }

            public bool? allowed { get; set; }
        }
    }
}
