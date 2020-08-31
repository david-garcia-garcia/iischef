using ACMESharp;
using iischef.core.IIS.AcmeProviders;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Threading;
using Exception = System.Exception;

namespace iischef.core.IIS
{
    /// <summary>
    /// Utility class to provision certificates for IIS
    /// </summary>
    public class SslCertificateProviderService
    {
        /// <summary>
        /// Store information about each individual certificate renewal status. We use this
        /// storage because the CCS might be shared between different servers
        /// and this information needs to be centralized.
        /// </summary>
        public class CertificateRenewalState
        {
            /// <summary>
            /// The certificate's domain name
            /// </summary>
            public string HostName { get; set; }

            /// <summary>
            /// When did the last renewal happen
            /// </summary>
            public DateTime? LastRenewal { get; set; }

            /// <summary>
            /// When is the next renewal planned for
            /// </summary>
            public DateTime? NextRenewal { get; set; }

            /// <summary>
            /// List of failed validations
            /// </summary>
            public List<DateTime> FailedValidations { get; set; }
        }

        /// <summary>
        /// The logging service
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// The chef applicaiton id
        /// </summary>
        protected string AppId;

        /// <summary>
        /// The global settings
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        /// <summary>
        /// Shared storage path. The path where shared acme sites are deployed.
        /// </summary>
        protected string StoragePath;

        /// <summary>
        /// Application pool utils
        /// </summary>
        protected UtilsAppPool AppPoolUtils;

        /// <summary>
        /// UtilsHosts
        /// </summary>
        protected UtilsHosts UtilsHosts;

        /// <summary>
        /// If this is a mock environment (AppVeyor, tests, etc.) the provisioning
        /// emits self-signed certificates.
        /// </summary>
        protected bool MockEnvironment;

        /// <summary>
        /// 
        /// </summary>
        protected Deployment Deployment;

        /// <summary>
        /// Renewal information for a certifixcate
        /// </summary>
        protected SimpleStore SimpleStoreRenewalStatus;

        /// <summary>
        /// Get an instance of SslCertificateProviderService
        /// </summary>
        public SslCertificateProviderService(
            ILoggerInterface logger,
            string appId,
            EnvironmentSettings globalSettings,
            Deployment deployment)
        {
            this.AppPoolUtils = new UtilsAppPool(logger);
            this.UtilsHosts = new UtilsHosts(logger);
            this.MockEnvironment = UtilsSystem.RunningInContinuousIntegration() || UnitTestDetector.IsRunningInTests || Debugger.IsAttached;

            // Everything performed against the staging API needs to be kept apart, including signer, etc...
            this.StoragePath = Path.Combine(globalSettings.GetDefaultContentStorage().path, "letsencrypt" + (this.MockEnvironment ? "_mock" : null));
            UtilsSystem.EnsureDirectoryExists(this.StoragePath, true);

            // If CCS is available, use that, otherwise use the central content storage
            string sslRenewalStateStorePath = UtilsIis.CentralStoreEnabled()
                ? UtilsIis.CentralStorePath(logger)
                : globalSettings.GetDefaultContentStorage().path;

            this.SimpleStoreRenewalStatus = new SimpleStore(Path.Combine(sslRenewalStateStorePath, "_ssl_renewal_state_store"), true);

            this.Logger = logger;
            this.AppId = appId;
            this.Deployment = deployment;
            this.GlobalSettings = globalSettings;
        }

        /// <summary>
        /// Get's a shared directory that will be used to store the ACME challenge responses
        /// </summary>
        /// <returns></returns>
        public string GetWellKnownSharedPathForApplication()
        {
            var result = Path.Combine(this.GetAcmeTemporarySiteRootForApplication(), ".well-known");
            UtilsSystem.DirectoryCreateIfNotExists(result);
            return result;
        }

        /// <summary>
        /// Get a shared site root for the application
        /// </summary>
        /// <returns></returns>
        public string GetAcmeTemporarySiteRootForApplication()
        {
            // Create a phantom website only to serve the file...
            var wellKnownSharedPath = Path.Combine(this.StoragePath, "webroot_" + this.AppId);
            UtilsSystem.DirectoryCreateIfNotExists(wellKnownSharedPath);

            // Grant specific user permissions
            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(
                this.Deployment.WindowsUsernameFqdn(),
                wellKnownSharedPath,
                FileSystemRights.ReadAndExecute,
                this.GlobalSettings.directoryPrincipal);

            return wellKnownSharedPath;
        }

        /// <summary>
        /// We store a target next renewal attempt, to avoid hitting let's encrypt api rate limits.
        /// </summary>
        /// <param name="hostName"></param>
        protected CertificateRenewalState GetCertificateRenewalState(string hostName)
        {
            // This is to support legacy storage migration, remove in future versions.
            var legacyValue = this.Deployment.GetSettingPersistent<DateTime?>("ssl-last-renewal-attempt" + "@" + hostName, null, this.Logger);

            string key = hostName.Replace(".", "_");

            if (this.SimpleStoreRenewalStatus.Get<CertificateRenewalState>(key, out var storeItem))
            {
                return storeItem.Data;
            }

            var result = new CertificateRenewalState()
            {
                HostName = hostName,
                NextRenewal = legacyValue
            };

            this.StoreCertificateRenewalState(result);
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        protected void StoreCertificateRenewalState(CertificateRenewalState state)
        {
            string key = state.HostName.Replace(".", "_");
            this.SimpleStoreRenewalStatus.Set(key, state);
        }

        /// <summary>
        /// Devuelve un provider según esté configurado en el sistema, se incorporó este
        /// método para soportar la transíción de ACMEV1 a ACMEV2
        /// </summary>
        /// <returns></returns>
        protected IAcmeSharpProvider GetAcmeProvider(ILoggerInterface logger, string hostName)
        {
            IAcmeSharpProvider result;

            // TODO: Quitar esto
            this.GlobalSettings.AcmeProvider = "certes";

            switch (this.GlobalSettings.AcmeProvider)
            {
                case "certes":
                    result = new AcmeSharpProviderCertes(logger, hostName, this.GlobalSettings);
                    break;
                default:
                    result = new AcmeSharpProvider(logger, hostName, this.GlobalSettings);
                    break;
            }

            this.Logger.LogInfo(true, "Using ACME provider: " + result.GetType().FullName);

            return result;
        }

        /// <summary>
        /// Provisions a certificate in the central store
        /// </summary>
        /// <param name="hostName">Domain to register</param>
        /// <param name="email">Registration e-mail</param>
        /// <param name="bindingInfo">IIS binding info</param>
        /// <param name="ownerSiteName">The site that owns the binding, used to assign identity and application pool permissions.</param>
        /// <param name="forceSelfSigned">Force a self-signed certificate</param>
        /// <param name="forceRenewal">Force renewal, even if renewal conditions are not met</param>
        /// <returns>The certificate's friendly name, ready to be bound in IIS</returns>
        public void ProvisionCertificateInIis(
            string hostName,
            string email,
            string bindingInfo,
            string ownerSiteName,
            bool forceSelfSigned = false,
            bool forceRenewal = false)
        {
            if (hostName.Contains("*"))
            {
                throw new Exception($"Provisioning certificates for wildcard host name '{hostName}' is not supported.");
            }

            var currentCertificate = UtilsIis.FindCertificateInCentralCertificateStore(hostName, this.Logger, out _);
            double remainingCertificateDays = (currentCertificate?.NotAfter - DateTime.Now)?.TotalDays ?? 0;

            this.Logger.LogInfo(true, "Total days remaining for certificate expiration: '{0}'", (int)Math.Floor(remainingCertificateDays));

            // Trigger renovation. Do this differently on mock/prod environment.
            // Next renewal attempt is calculated based on previous renewal attempt
            var renewalState = this.GetCertificateRenewalState(hostName);

            // Legacy apps don't have this set, or when a certificate has been manually placed
            if (renewalState.NextRenewal == null && remainingCertificateDays > 1)
            {
                renewalState.NextRenewal = this.CalculateNextRenewalAttempt(hostName, (int)remainingCertificateDays);
                this.StoreCertificateRenewalState(renewalState);
            }

            int remainingDaysForNextRenewal = renewalState.NextRenewal == null ? 0 : (int)(renewalState.NextRenewal - DateTime.UtcNow).Value.TotalDays;
            int certificateTotalDuration = currentCertificate == null ? 0 : (int)(currentCertificate.NotAfter - currentCertificate.NotBefore).TotalDays;

            this.Logger.LogInfo(true, "Next renewal attempt for this site SSL targeted in '{0}' days.", remainingDaysForNextRenewal);

            // Check that the validationfailed request rate is not exceeded for this domain
            if (!forceRenewal && renewalState.FailedValidations.AsIterable().Count(i => (DateTime.UtcNow - i).TotalHours < 48) >= 2)
            {
                // Make this message verbos so that it will not flood the logs, the failed validation message will get logged
                // anyways and is sufficient.
                this.Logger.LogWarning(true, "The hostname '{0}' has reached the limit of two failed validations in the last 48 hours.", hostName);
                return;
            }

            if (!forceRenewal && !forceSelfSigned && remainingDaysForNextRenewal > 0 && remainingCertificateDays > 0)
            {
                this.Logger.LogWarning(true, "Next renewal attempt date not reached, skipping SSL provisioning.");
                return;
            }

            if (!forceRenewal && remainingDaysForNextRenewal > 0 && (remainingDaysForNextRenewal > certificateTotalDuration * 0.5) && certificateTotalDuration > 0)
            {
                this.Logger.LogWarning(false, "Certificate has not yet been through at least 50% of it's lifetime so it will not be renewed.'");
                renewalState.NextRenewal = this.CalculateNextRenewalAttempt(hostName, (int)remainingCertificateDays);
                this.StoreCertificateRenewalState(renewalState);
                return;
            }

            // Check the general too many requests rate exceeded
            if (!forceRenewal && this.SimpleStoreRenewalStatus.Get<bool>("ssl-certificate-provider-too-many-requests", out var tooManyRequests))
            {
                this.Logger.LogWarning(false, "Certificate provisioning temporarily disabled due to a Too Many Requests ACME error. Flag stored in {0}", tooManyRequests.StorePath);
                return;
            }

            this.Logger.LogInfo(false, "Attempting SSL certificate renewal for site '{0}' and host '{1}'", ownerSiteName, hostName);

            // Clear old validation failed requests
            if (renewalState.FailedValidations?.Any() == true)
            {
                // Only keep failed validations that happen in the last 5 days
                renewalState.FailedValidations = renewalState.FailedValidations
                    .Where((i) => (DateTime.UtcNow - i).TotalDays < 5).ToList();
            }

            // This is a little bit inconvenient but... the most reliable and compatible
            // way to do this is to setup a custom IIS website that uses the binding during
            // provisioning.

            long tempSiteId;
            List<Site> haltedSites = new List<Site>();

            var tempSiteName = "cert-" + this.AppId;
            var tempSiteAppId = "cert-" + this.AppId;
            string tempHostName = "localcert-" + hostName;

            this.Logger.LogInfo(true, "Preparing temp site: " + tempSiteName);

            List<Site> conflictingSites;

            // Prepare the site
            using (ServerManager sm = new ServerManager())
            {
                // Query the sites in a resilient way...
                conflictingSites = UtilsSystem.QueryEnumerable(
                    sm.Sites,
                    (s) => s.State == ObjectState.Started && s.Bindings.Any((i) => i.Host.Equals(hostName)),
                    (s) => s,
                    (s) => s.Name,
                    this.Logger);
            }

            // Stop the sites that might prevent this one from starting
            foreach (var s in conflictingSites)
            {
                this.Logger.LogInfo(true, "Stopping site {0} to avoid binding collision.", s.Name);
                this.AppPoolUtils.WebsiteAction(s.Name, AppPoolActionType.Stop, skipApplicationPools: true);
                haltedSites.Add(s);
            }

            using (ServerManager sm = new ServerManager())
            {
                // Make sure there is no other site (might be stuck?)
                var existingSite = (from p in sm.Sites
                                    where p.Name == tempSiteName
                                    select p).FirstOrDefault();

                var tempSite = existingSite ?? sm.Sites.Add(tempSiteName, this.GetAcmeTemporarySiteRootForApplication(), 80);

                // Propagate application pool usage so that permissions are properly handled.
                var ownerSite = sm.Sites.First((i) => i.Name == ownerSiteName);
                tempSite.Applications.First().ApplicationPoolName = ownerSite.Applications.First().ApplicationPoolName;

                // Delete all bindings
                tempSite.Bindings.Clear();

                tempSite.Bindings.Add(bindingInfo, "http");
                tempSite.Bindings.Add($"{UtilsIis.LOCALHOST_ADDRESS}:80:" + tempHostName, "http");
                tempSiteId = tempSite.Id;

                this.UtilsHosts.AddHostsMapping(UtilsIis.LOCALHOST_ADDRESS, tempHostName, tempSiteAppId);

                // Prepare the website contents
                var sourceDir = UtilsSystem.FindResourcePhysicalPath(typeof(IISDeployer), ".well-known");
                UtilsSystem.CopyFilesRecursively(new DirectoryInfo(sourceDir), new DirectoryInfo(this.GetWellKnownSharedPathForApplication()), true);

                UtilsIis.CommitChanges(sm);
            }

            UtilsIis.WaitForSiteToBeAvailable(tempSiteName, this.Logger);
            UtilsIis.ConfigureAnonymousAuthForIisApplication(tempSiteName, this.Deployment.WindowsUsernameFqdn(), this.Deployment.GetWindowsPassword());

            IAcmeSharpProvider provider = null;

            try
            {
                this.AppPoolUtils.WebsiteAction(tempSiteName, AppPoolActionType.Start);

                // Check that the site does work using the local binding
                var testDataUrl = $"http://{tempHostName}/.well-known/acme-challenge/test.html";
                this.Logger.LogInfo(true, "Validating local challenge setup at: {0}", testDataUrl);

                if (!string.Equals(UtilsSystem.DownloadUriAsText(testDataUrl), "test data"))
                {
                    throw new Exception($"Could not locally validate acme challenge site setup at {testDataUrl}");
                }

                // Ssl registration configuration only depends on the e-mail and is signed as such
                string sslSignerAndRegistrationStoragePath = UtilsSystem.EnsureDirectoryExists(
                    UtilsSystem.CombinePaths(this.StoragePath, "_ssl_config", StringFormating.Instance.ExtremeClean(email)), true);

                // Initialize the provider
                bool useMockProvider = this.MockEnvironment || forceSelfSigned;

                provider = useMockProvider
                    ? (IAcmeSharpProvider)new AcmeSharpProviderMock(this.Logger, tempHostName)
                    : this.GetAcmeProvider(this.Logger, hostName);

                var signerPath = Path.Combine(this.StoragePath, "_signer.xml");
                var registrationPath = Path.Combine(sslSignerAndRegistrationStoragePath, "registration.json");

                provider.InitRegistration(signerPath, registrationPath, email);

                string challengeUrl;
                string challengeContent;
                string challengeFilePath;

                try
                {
                    provider.GenerateHttpChallenge(
                        out challengeUrl,
                        out challengeContent,
                        out challengeFilePath);
                }
                catch (AcmeClient.AcmeWebException acmeException)
                {
                    if (acmeException.Message.Contains("429"))
                    {
                        int waitHours = 1;
                        this.SimpleStoreRenewalStatus.Set("ssl-certificate-provider-too-many-requests", true, 60 * waitHours);
                        this.Logger.LogError("Let's encrypt too many requests issue. Certificate provisioning disabled for the next {0} hours.", waitHours);
                        this.Logger.LogException(acmeException, EventLogEntryType.Warning);
                        return;
                    }

                    throw;
                }

                // Write the challanege contents
                string challengeFullPath =
                    Path.Combine(this.GetAcmeTemporarySiteRootForApplication(), challengeFilePath);

                File.WriteAllText(
                    challengeFullPath,
                    challengeContent);

                this.Logger.LogInfo(false, $"Veryfing challenge at '{challengeUrl}'");

                try
                {
                    // Validate that we can actually access the challenge ourselves!
                    string contents = UtilsSystem.DownloadUriAsText(challengeUrl, false);

                    if (!string.Equals(contents, challengeContent))
                    {
                        throw new Exception(
                            $"Could not validate ACME challenge, retrieved challenge '{contents}' does not match '{challengeContent}'");
                    }
                }
                catch (Exception e)
                {
                    this.Logger.LogWarning(true, "Cannot self-verify auth challenge, this can sometimes happeen under some DNS setups. {0}", e.Message + Environment.NewLine + e.InnerException?.Message);
                }

                var challengeValidated = false;

                try
                {
                    challengeValidated = provider.ValidateChallenge();
                }
                catch (Exception e)
                {
                    this.Logger.LogException(e, EventLogEntryType.Warning);
                }

                this.Logger.LogWarning(true, "Remote challenge validation success: " + (challengeValidated ? "Yes" : "No"));

                // Download the certificates to this temp location
                string temporaryCertificatePath = UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.StoragePath, this.AppId, "ssl_certificates", hostName), true);
                CertificatePaths certificatepaths = null;

                // This is here for testing purposes
                if (Environment.GetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION") == true.ToString())
                {
                    challengeValidated = false;
                }

                if (!challengeValidated)
                {
                    // There is a Failed Validation limit of 5 failures per account, per hostname, per hour.
                    renewalState.FailedValidations = renewalState.FailedValidations ?? new List<DateTime>();
                    renewalState.FailedValidations.Add(DateTime.UtcNow);

                    this.Logger.LogError(
                        "Challenge could not be validated at '{0}'. If behind a load balancer, make sure that the site is deployed in ALL nodes, remove the self-signed certificate from the store and redeploy the application.",
                        challengeUrl);

                    this.StoreCertificateRenewalState(renewalState);
                }
                else
                {
                    try
                    {
                        certificatepaths = provider.DownloadCertificate(
                            UtilsEncryption.GetMD5(hostName),
                            hostName,
                            temporaryCertificatePath);
                    }
                    catch (AcmeClient.AcmeWebException acmeException)
                    {
                        this.Logger.LogException(acmeException, EventLogEntryType.Warning);
                    }
                    catch (WebException webException)
                    {
                        this.Logger.LogException(webException, EventLogEntryType.Warning);
                    }
                    catch (Exception e)
                    {
                        this.Logger.LogException(e, EventLogEntryType.Warning);
                    }
                }

                if (certificatepaths == null && currentCertificate == null)
                {
                    this.Logger.LogWarning(false, "Unable to acquire certificate and site does not have a valid existing one, using self-signed fallback.");

                    provider = new AcmeSharpProviderMock(this.Logger, hostName);

                    certificatepaths = provider.DownloadCertificate(
                        UtilsEncryption.GetMD5(hostName),
                        hostName,
                        temporaryCertificatePath);
                }

                // Save this, use a fixed name certificate file
                if (certificatepaths != null)
                {
                    string certificateFilePath = Path.Combine(UtilsIis.CentralStorePath(this.Logger), hostName + ".pfx");

                    UtilsSystem.RetryWhile(
                        () => { File.Copy(certificatepaths.pfxPemFile, certificateFilePath, true); },
                        (e) => true,
                        2500,
                        this.Logger);

                    this.Logger.LogInfo(false, "Certificate file writen to '{0}'", certificateFilePath);

                    // TODO: Activate this refreshing when it's prooved to work
                    // UtilsIis.EnsureCertificateInCentralCertificateStoreIsRebound(hostName, this.Logger);
                }

                // Remove temporary certificates
                UtilsSystem.DeleteDirectory(temporaryCertificatePath, this.Logger);

                // Remove the already used challenge if it was validated. Otherwise keep it
                // for debugging purposes.
                if (challengeValidated && File.Exists(challengeFullPath))
                {
                    File.Delete(challengeFullPath);
                }

                // In the end, we always have a certificate. Program a renewal date according to the remaining expiration.
                currentCertificate = UtilsIis.FindCertificateInCentralCertificateStore(hostName, this.Logger, out _);
                remainingCertificateDays = (currentCertificate?.NotAfter - DateTime.Now)?.TotalDays ?? 0;

                // Add some randomness in renewal dates to avoid all certificates being renewed at once and reaching api limits
                renewalState.LastRenewal = DateTime.UtcNow;
                renewalState.NextRenewal = this.CalculateNextRenewalAttempt(hostName, (int)remainingCertificateDays);
                this.StoreCertificateRenewalState(renewalState);
            }
            finally
            {
                this.Logger.LogInfo(true, "Disposing temporary verification setup");

                provider?.Dispose();

                this.UtilsHosts.RemoveHostsMapping(tempSiteAppId);

                // Restore the original state of IIS!!!
                using (ServerManager sm = new ServerManager())
                {
                    var site = sm.Sites.Single(i => i.Id == tempSiteId);
                    UtilsIis.RemoveSite(site, sm, this.Logger);
                    UtilsIis.CommitChanges(sm);
                }

                // Give IIS some time to reconfigure itself and free resources.
                Thread.Sleep(1000);

                // Start the sites
                foreach (var site in haltedSites)
                {
                    // Add some retry logic here because bringing the original sites online is critical
                    UtilsSystem.RetryWhile(() => { this.AppPoolUtils.WebsiteAction(site.Name, AppPoolActionType.Start); }, (e) => true, 5000, this.Logger);
                }
            }
        }

        /// <summary>
        /// Calculates a semi-random next renewal attempt date
        /// for the certificate based on the remaining number of certificate days.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="remainingCertificateDays"></param>
        protected DateTime CalculateNextRenewalAttempt(string hostName, int remainingCertificateDays)
        {
            // Add some randomness in renewal dates to avoid all certificates being renewed at once and reaching api limits
            int lowerLimit = (int)(remainingCertificateDays / 2);
            int upperLimit = (int)(remainingCertificateDays - 30);

            if (upperLimit < lowerLimit)
            {
                upperLimit = lowerLimit;
            }

            var nextRenewalDate = DateTime.UtcNow.AddDays((new Random()).Next(lowerLimit, upperLimit));

            // Safe-guard to prevent tight renewal loops
            int remainingDays = (int)(nextRenewalDate - DateTime.UtcNow).TotalDays;

            if (remainingDays < 2)
            {
                // Something between 1 and 4
                nextRenewalDate = DateTime.UtcNow.AddDays((new Random().Next(1, 4)));
            }

            int daysUntilRenewal = (int)(nextRenewalDate - DateTime.UtcNow).TotalDays;
            int daysRenewalBeforeItExpires = (int)(DateTime.UtcNow.AddDays(remainingCertificateDays) - nextRenewalDate).TotalDays;

            this.Logger.LogWarning(false, "Next renewal attempt in {0} days for host '{1}' ({2} UTC)", daysUntilRenewal, hostName, nextRenewalDate);
            this.Logger.LogWarning(false, "Certificate will be renewed {0} days before it expires.", daysRenewalBeforeItExpires);

            return nextRenewalDate;
        }
    }
}
