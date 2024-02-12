namespace iischef.core.IIS
{
    /// <summary>
    /// Convenience class to store PFX and its password together
    /// </summary>
    public class AuthenticatedPFX
    {
        /// <summary>
        /// Constructor for the class
        /// </summary>
        /// <param name="pfxFullPath"></param>
        /// <param name="pemCertPath"></param>
        /// <param name="pemKeyPath"></param>
        public AuthenticatedPFX(string pfxFullPath, string pemCertPath, string pemKeyPath, string password)
        {
            this.PfxPassword = password;
            this.PfxFullPath = pfxFullPath;
            this.PemCertPath = pemCertPath;
            this.PemKeyPath = pemKeyPath;
        }

        /// <summary>
        /// Full path to the pfx, including the PFX
        /// </summary>
        public string PfxFullPath { get; private set; }

        /// <summary>
        /// PFX password
        /// </summary>
        public string PfxPassword { get; private set; }

        /// <summary>
        /// Full path to the PEM certificate
        /// </summary>
        public string PemCertPath { get; private set; }

        /// <summary>
        /// Full path to the PEM private key
        /// </summary>
        public string PemKeyPath { get; private set; }
    }
}
