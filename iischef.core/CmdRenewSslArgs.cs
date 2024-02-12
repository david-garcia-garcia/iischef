namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class CmdRenewSslArgs
    {
        /// <summary>
        /// Certificate provider to use
        /// </summary>
        public CertProvider? CertProvider { get; set; }

        /// <summary>
        /// Number of days left on existing certificate before it is renewed
        /// </summary>
        public int RenewThresholdDays { get; set; } = 10;

        /// <summary>
        /// The hostname
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string RegistrationMail { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string IssuerName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseSelfSignedFallbackIfNoPfx { get; set; }

        /// <summary>
        /// Verify that hostname has DNS
        /// </summary>
        public bool DisableDnsValidation { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum CertProvider
    {
        /// <summary>
        /// SelfSigned
        /// </summary>
        SelfSigned = 0,

        /// <summary>
        /// Acme
        /// </summary>
        Acme = 1,

        /// <summary>
        /// AcmeStaging
        /// </summary>
        AcmeStaging = 3
    }
}
