using iischef.core;
using iischef.logger;
using iischef.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;

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
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefCcsCleanup")]
    public class IISChefCcsCleanup : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// Delete unused certificates that expired more than ExpireThreshold days ago.
        /// </summary>
        [Parameter]
        public int ExpireThreshold { get; set; } = 30;

        /// <summary>
        /// 
        /// </summary>
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);

            var siteBindings = UtilsIis.ParseSiteBindings(logger, null, "https");

            var appSettings = new ApplicationDataStore();

            List<string> certificatesInUse = new List<string>();

            foreach (var s in siteBindings)
            {
                var cert = UtilsIis.FindCertificateInCentralCertificateStore(
                    s.Binding.Hostname,
                    logger,
                    appSettings.AppSettings.PfxPassword,
                    out var certificatePath);

                if (certificatePath != null)
                {
                    certificatesInUse.Add(certificatePath);
                }
            }

            var ccsLocation = UtilsIis.GetCentralCertificateStorePathRaw(logger);

            var certificates = Directory
                .EnumerateFiles(ccsLocation, "*.pfx")
                .ToList();

            foreach (var certificate in certificates)
            {
                try
                {
                    using (var cert = new X509Certificate2(certificate, string.IsNullOrWhiteSpace(appSettings.AppSettings.PfxPassword) ? null : appSettings.AppSettings.PfxPassword))
                    {
                        var expiredDaysAgo
                            = Math.Round((DateTime.UtcNow - cert.NotAfter).TotalDays, 0);

                        if (expiredDaysAgo > this.ExpireThreshold)
                        {
                            if (certificatesInUse.Any((i) =>
                                    i.Equals(certificate, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                logger.LogError(
                                    $"Expired {Path.GetFileName(certificate)} expired {expiredDaysAgo} days ago with subject {cert.Subject} still in use.");

                                continue;
                            }

                            // Expired certificate, remove
                            logger.LogWarning(
                                false,
                                $"Deleting certificate {Path.GetFileName(certificate)} expired {expiredDaysAgo} days ago with subject {cert.Subject}");

                            File.Delete(certificate);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Unable to read certificate {Path.GetFileName(certificate)}: " + ex.Message);
                }
            }
        }
    }
}
