using iischef.core;
using iischef.logger;
using iischef.utils;
using System;
using System.IO;
using System.Linq;
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
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefCcsChangePrivateKeyPassword")]
    public class IISChefCcsChangePrivateKeyPassword : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// Existing password for certificate private key. Provide this along with CertificatePassword
        /// to encode al existing certificates with a new password.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Existing { get; set; }

        /// <summary>
        /// Optional, if nothing specificed it will use the currently configured password
        /// for CCS
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string New { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);

            var appSettings = new ApplicationDataStore();

            if (this.New == null)
            {
                this.New = appSettings.AppSettings.PfxPassword;

                if (this.New == null)
                {
                    throw new BusinessRuleException($"Could not determine new password for certificates. Please setup one for CCS using Invoke-IISChefSetupCcs or provide one through the New parameter.");
                }
            }

            var ccsLocation = UtilsIis.GetCentralCertificateStorePathRaw(logger);

            var certificates = Directory
                .EnumerateFiles(ccsLocation, "*.pfx")
                .ToList();

            foreach (var pfx in certificates)
            {
                string certificateFileName = new FileInfo(pfx).Name;

                // This pwd is OK
                if (UtilsCertificate.CheckPfxPassword(pfx, this.New))
                {
                    continue;
                }

                var passwordWorksForCert =
                    UtilsCertificate.CheckPfxPassword(pfx, this.Existing);

                if (!passwordWorksForCert)
                {
                    logger.LogWarning(false, $"Provided password does not work with certificate file: {certificateFileName}");
                    continue;
                }

                var pfxBackup = pfx + $".{DateTime.UtcNow.ToUnixTimestamp()}.bak";
                File.Copy(pfx, pfxBackup);

                UtilsCertificate.ChangePrivateKeyPwd(
                    pfx,
                    this.Existing,
                    this.New);

                logger.LogWarning(false, $"Certificate {certificateFileName} password was updated.");
            }
        }
    }
}
