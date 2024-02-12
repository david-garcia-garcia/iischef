using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace iischef.core.IIS
{
    /// <summary>
    /// 
    /// </summary>
    public static class SslBindingSync
    {
        /// <summary>
        /// Sincroniza todos los nombres de certificado que haya en el CCS (central certificate store)
        /// hacia un site
        /// </summary>
        /// <param name="siteName"></param>
        public static void SyncCcsBindingsToSite(string siteName, ILoggerInterface logger)
        {
            var ccsPath = new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger));

            var fileNamesWithoutExtension = ccsPath.EnumerateFiles("*.pfx").Select((i) => Path.GetFileNameWithoutExtension(i.Name)).ToList();

            using (ServerManager sm = new ServerManager())
            {
                var existingSite = (from p in sm.Sites
                                    where p.Name == siteName
                                    select p).FirstOrDefault();

                bool changed = false;

                if (existingSite == null)
                {
                    throw new BusinessRuleException($"Site not found: {siteName}");
                }

                foreach (var file in fileNamesWithoutExtension)
                {
                    if (!CcsFileNameToHttpsBinding(file, out string bInfo))
                    {
                        logger.LogWarning(false, $"The PFX certificate {file} bound to the site {siteName} has an invalid format.");
                        continue;
                    }

                    // Check that the CCS password matches the actual certificate password, just to aid in debugging pwd mismatch issues,
                    // specially knowing that IIS will NOT give any feedback when this happens or there is NO way
                    // to retrieve this from the console (you can see it on the GUI, but remote IIS
                    // to containers does not support the CCS part).
                    var pfxPassword = new ApplicationDataStore().AppSettings.PfxPassword;

                    if (pfxPassword != null)
                    {
                        if (!UtilsCertificate.CheckPfxPassword(Path.Combine(ccsPath.FullName, file) + ".pfx", pfxPassword))
                        {
                            logger.LogWarning(
                                false,
                                $"The PFX certificate {file} has a password incompatible with current CCS stored credentials. Use Invoke-ChefAppSetupIisCert to update the credentials.");
                        }
                    }
                    else
                    {
                        logger.LogWarning(false, $"No certificate password has been set, PFX compatiblity cannot be tested. Please use Invoke-ChefAppSetupIisCert to set a certificate password.");
                    }

                    bool exists = (from binding in existingSite.Bindings
                                   where binding.BindingInformation == bInfo
                                         && binding.Protocol == "https"
                                   select 1).Any();

                    if (exists)
                    {
                        continue;
                    }

                    changed = true;

                    logger.LogInfo(false, $"Added ssl binding {bInfo} for site {siteName} to match certificates in CCS.");

                    existingSite.Bindings.Add(bInfo, null, "MY", SslFlags.CentralCertStore | SslFlags.Sni);
                }

                if (changed)
                {
                    logger.LogInfo(false, $"Updated ssl bindings for site {siteName} to match certificates in CCS.");
                    sm.CommitChanges();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool CcsFileNameToHttpsBinding(string fileName, out string binding)
        {
            binding = null;

            var hostName = fileName.Replace("_", "*");

            if (SslBindingSync.ValidateHostnameInSslBinding(hostName) == false)
            {
                return false;
            }

            binding = $"*:443:{hostName}";
            return true;
        }

        /// <summary>
        /// Ensure that a hostname in an IIS binding is valid. Looking to deal
        /// with missusing wildcards
        /// </summary>
        /// <param name="hostname"></param>
        /// <returns></returns>
        public static bool ValidateHostnameInSslBinding(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            // Para no liarla, validamos el hostname
            return Regex.IsMatch(
                hostname,
                "^(?:(\\*\\.)+)?([a-zA-Z0-9]+(-[a-zA-Z0-9]+)*\\.)+[a-zA-Z]{2,}$",
                RegexOptions.Compiled,
                TimeSpan.FromSeconds(4));
        }
    }
}
