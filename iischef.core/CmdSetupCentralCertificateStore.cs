using iischef.logger;
using iischef.utils;
using iischef.utils.WindowsAccount;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class CmdSetupCentralCertificateStore
    {
        public const string CcsDefaultUserName = "iis_ccs_store";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <param name="logger"></param>
        public void Run(CmdSetupCentralCertificateStoreArgs args, ILoggerInterface logger)
        {
            if (!UtilsSystem.IsWindowsFeatureEnabled(IISFeatureNames.CentralCertificateStore, logger))
            {
                throw new BusinessRuleException("Central certificate store not enabled on this computer.");
            }

            if (string.IsNullOrWhiteSpace(args.CertStoreLocation))
            {
                args.CertStoreLocation = UtilsIis.GetCentralCertificateStorePathRaw(logger);

                if (string.IsNullOrWhiteSpace(args.CertStoreLocation))
                {
                    throw new BusinessRuleException(
                        "CertStoreLocation not specified, and no location is currently configured.");
                }

                logger.LogInfo(true, "Using existing CCS location: " + args.CertStoreLocation);
            }

            UtilsSystem.EnsureDirectoryExists(args.CertStoreLocation, true);

            bool setCertificatePassword =
                args.PrivateKeyPassword != null;

            var certificates = Directory
                .EnumerateFiles(args.CertStoreLocation, "*.pfx")
                .ToList();

            if (setCertificatePassword)
            {
                // Make sure that certs can be opened, otherwise try to re-encode
                foreach (var pfx in certificates)
                {
                    string certificateFileName = new FileInfo(pfx).Name;

                    // This pwd is OK
                    if (UtilsCertificate.CheckPfxPassword(pfx, args.PrivateKeyPassword))
                    {
                        continue;
                    }

                    logger.LogWarning(false, $"Certificate {certificateFileName} does not work with provided private key password. Use IISChefCcsChangePrivateKeyPassword to update the certificate passwords.");
                }
            }

            var appSettings = new ApplicationDataStore();

            // Start determining the username to use
            var userName = args.UserName;
            var currentlyConfiguredUsername = UtilsIis.GetCentralCertificateStoreUserNameRaw(logger);

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = currentlyConfiguredUsername;

                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = CcsDefaultUserName;
                }
            }

            if (args.RegenerateStoreAccount == true)
            {
                if (!string.IsNullOrWhiteSpace(args.UserName) || !string.IsNullOrWhiteSpace(args.Password))
                {
                    throw new BusinessRuleException($"When regenerating store account, UserName and Password must not be provided. The system will auto generate them.");
                }

                // Reset so that credentials will be assigned later
                userName = CcsDefaultUserName;
                appSettings.AppSettings.CcsAccountPassword = null;
            }

            bool localUserExists = UtilsAccountManagement.UserExists(userName, logger);

            // Check that, if using automanaged account, it does exist
            if (userName == CcsDefaultUserName)
            {
                // Generate a password if it does not have one
                if (appSettings.AppSettings.CcsAccountPassword == null)
                {
                    appSettings.AppSettings.CcsAccountPassword = PasswordHelper.GenerateRandomPassword(15);
                }

                if ((args.RegenerateStoreAccount == true || !localUserExists))
                {
                    if (!localUserExists)
                    {
                        logger.LogWarning(
                            false,
                            $"A local account to access the certificates has been created. Use the {nameof(args.RegenerateStoreAccount)} flag in the future if you need to renegerate the account.");
                    }

                    UtilsAccountManagement.UpsertUser(userName, appSettings.AppSettings.CcsAccountPassword, logger);
                }
            }

            if (args.Password != null)
            {
                appSettings.AppSettings.CcsAccountPassword = args.Password;
            }

            bool credentialsWork = UtilsAccountManagement.CheckUserAndPassword(userName, appSettings.AppSettings.CcsAccountPassword, logger);

            if (!credentialsWork)
            {
                throw new BusinessRuleException($"Currently known or provided credentials for the certificate storage account '{userName}' do not work. You can use the {nameof(args.RegenerateStoreAccount)} flag to for automatic account generation.");
            }

            UtilsAccountManagement.EnsureUserInGroup(userName, UtilsWindowsAccounts.WELL_KNOWN_SID_USERS, logger);

            if (args.InstallLetsEncryptChainToCertUser)
            {
                if (!localUserExists)
                {
                    throw new Exception($"Cannot install certificates to user {userName} because it is not a local user or the user does not exist.");
                }

                try
                {
                    this.AddLetsEncryptAuthoritiesToAccountAndMachineStore(userName, appSettings.AppSettings.CcsAccountPassword, null, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Unable to add Let's encrypt root certificates to {userName}: " + ex.Message);
                }
            }

            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddCommand("Enable-WebCentralCertProvider");

                ps.AddParameter("CertStoreLocation", args.CertStoreLocation);
                ps.AddParameter("UserName", userName);
                ps.AddParameter("Password", appSettings.AppSettings.CcsAccountPassword);

                // The Enable-WebCentralCertProvider won't let clearing passwords.
                if (setCertificatePassword && !string.IsNullOrWhiteSpace(args.PrivateKeyPassword))
                {
                    ps.AddParameter("PrivateKeyPassword", args.PrivateKeyPassword);
                }

                ps.InvokeAndTreatError(logger);
            }

            // The cmdlet won't allow the password to be null, we need to do this manually.
            if (setCertificatePassword && string.IsNullOrWhiteSpace(args.PrivateKeyPassword))
            {
                logger.LogWarning(
                    false, "You cannot set an empty password for the private key using this command. Use IIS interface to complete the process.");
            }

            if (setCertificatePassword)
            {
                if (appSettings.AppSettings.PfxPassword != null)
                {
                    if (appSettings.AppSettings.PfxPassword != args.PrivateKeyPassword)
                    {
                        logger.LogInfo(false, $"CCS stored Private Key Password updated.");
                    }
                }
                else
                {
                    logger.LogInfo(false, $"CCS stored Private Key Password initialized.");
                }

                appSettings.AppSettings.PfxPassword = args.PrivateKeyPassword;
            }

            appSettings.Save();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <param name="logger"></param>
        private void AddLetsEncryptAuthoritiesToAccountAndMachineStore(
            string userName,
            string password,
            string domain,
            ILoggerInterface logger)
        {
            List<string> certificateUrls = new List<string>();

            certificateUrls.Add("https://letsencrypt.org/certs/isrg-root-x1-cross-signed.pem");
            certificateUrls.Add("https://letsencrypt.org/certs/lets-encrypt-r3.pem");

            List<X509Certificate2> certificates = new List<X509Certificate2>();

            foreach (var certificateUrl in certificateUrls)
            {
                // Download the certificate
                WebClient client = new WebClient();

                byte[] certBytes = client.DownloadData(certificateUrl);
                certificates.Add(new X509Certificate2(certBytes));
            }

            if (string.IsNullOrWhiteSpace(domain))
            {
                domain = ".";
            }

            Advapi32Extern.LogonUser(userName, domain, password, 2, 0, out var accessToken);

            List<X509Certificate2> missingCertificates = new List<X509Certificate2>();

            WindowsIdentity.RunImpersonated(accessToken, () =>
            {
                using (X509Store userStore = new X509Store("My", StoreLocation.CurrentUser))
                {
                    userStore.Open(OpenFlags.ReadOnly);

                    foreach (var c in certificates)
                    {
                        if (!userStore.Certificates.Contains(c))
                        {
                            missingCertificates.Add(c);
                        }
                    }

                    userStore.Close();
                }
            });

            if (missingCertificates.Count > 0)
            {
                UtilsAccountManagement.EnsureUserInGroup(userName, UtilsWindowsAccounts.WELL_KNOWN_SID_ADMINISTRATORS, logger);
            }

            foreach (var missingCertificate in missingCertificates)
            {
                logger.LogInfo(false, $"Importing certificate to [{userName}\\My]: " + missingCertificate.Subject);

                var tempPath = Path.Combine(Environment.GetEnvironmentVariable("PUBLIC"), Guid.NewGuid().ToString() + "tmp");

                try
                {
                    File.WriteAllBytes(tempPath, missingCertificate.GetRawCertData());
                    CertificateImportUtils.ImportCertificateToStore(userName, null, password, tempPath, logger);
                }
                finally
                {
                    File.Delete(tempPath);
                }
            }

            // Remove from the admin group
            UtilsAccountManagement.EnsureUserNotInGroup(userName, UtilsWindowsAccounts.WELL_KNOWN_SID_ADMINISTRATORS, logger);

            // This is a workaround to initialize the user's certificate store
            // this.InitializeSessionData(userName, password, domain, logger);

            List<X509Certificate2> missingMachineCertificates = new List<X509Certificate2>();

            // Add to the machine store
            using (X509Store machineStore = new X509Store("My", StoreLocation.LocalMachine))
            {
                machineStore.Open(OpenFlags.ReadWrite);

                foreach (var c in certificates)
                {
                    if (!machineStore.Certificates.Contains(c))
                    {
                        missingMachineCertificates.Add(c);
                    }
                }

                foreach (var missingCertificate in missingMachineCertificates)
                {
                    logger.LogInfo(false, "Importing certificate to LocalMachine\\My: " + missingCertificate.Subject);
                    machineStore.Add(missingCertificate);
                }

                machineStore.Close();
            }
        }
    }
}
