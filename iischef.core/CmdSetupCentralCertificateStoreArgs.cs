namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class CmdSetupCentralCertificateStoreArgs
    {
        /// <summary>
        /// The certificate store location
        /// </summary>
        public string CertStoreLocation { get; set; }

        /// <summary>
        /// New password for certificate private key.
        /// </summary>
        public string PrivateKeyPassword { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool InstallLetsEncryptChainToCertUser { get; set; }

        /// <summary>
        /// Regenerate the user account used for the CCS
        /// </summary>
        public bool? RegenerateStoreAccount { get; set; }
    }
}
