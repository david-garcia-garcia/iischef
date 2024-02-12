using System;

namespace iischef.core.IIS
{
    /// <summary>
    /// Commodity class to store Certes settings
    /// </summary>
    public class CertesSettings
    {
        /// <summary>
        /// 
        /// </summary>
        public byte[] Key { get; set; }

        /// <summary>
        /// Uri of ACME api test or real
        /// </summary>
        public Uri ServiceUri { get; set; }

        /// <summary>
        /// Mail of account in ACME
        /// </summary>
        public string AccountEmail { get; set; }
    }
}
