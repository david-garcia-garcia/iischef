using iischef.core;
using iischef.logger;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Setup Central Certificate store for IIS on a local directory,
    /// with only access for an automatically generated local account + store
    /// an encripted password for certificates.
    /// 
    /// Through experience we found out that directly using a shared drive
    /// to store certificates is a very bad idea. Any glitch when reading
    /// the certificate source completely breaks certificates for serveral
    /// minutes, needeing a full IIS reset to repick the new certificates.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefSetupCcs")]
    public class IISChefSetupCcs : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// The certificate store location
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string CertStoreLocation { get; set; }

        /// <summary>
        /// New password for certificate private key.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string PrivateKeyPassword { get; set; }

        /// <summary>
        /// Username for the ccs account
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string UserName { get; set; }

        /// <summary>
        /// Password for the ccs account. Use NULL or leave empty to ignore. Use
        /// an empty string to have password-less certificates.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string Password { get; set; }

        /// <summary>
        /// Regenerate the user account used for the CCS
        /// </summary>
        [Parameter]
        public SwitchParameter RegenerateStoreAccount { get; set; }

        /// <summary>
        /// Esto en realidad no hace falta para que los certificados funcionen, pero
        /// desde un punto de vista de administración es interesante pq sino se hace
        /// esto en el IIS Manager no aparecen los detalles de los certificados
        /// que hay en el CCS. Al pasar a contenedores estó dejó de tener sentido
        /// porque el IIS remoto NO tiene la vista del CCS, y además daba problemas
        /// en containers así que se controla con un flag.
        /// </summary>
        [Parameter]
        public SwitchParameter InstallLetsEncryptChainToCertUser { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);
            var cmd = new CmdSetupCentralCertificateStore();

            var args = new CmdSetupCentralCertificateStoreArgs()
            {
                CertStoreLocation = this.CertStoreLocation,
                RegenerateStoreAccount = this.RegenerateStoreAccount.IsPresent ? this.RegenerateStoreAccount : (bool?)null,
                PrivateKeyPassword = this.PrivateKeyPassword,
                InstallLetsEncryptChainToCertUser = this.InstallLetsEncryptChainToCertUser.IsPresent,
                UserName = this.UserName,
                Password = this.Password
            };

            cmd.Run(args, logger);
        }
    }
}
