using iischef.core.IIS.AcmeProviders;
using iischef.core.IIS.Exceptions;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Exception = System.Exception;

namespace iischef.core.IIS
{
    /// <summary>
    /// Utility class to provision certificates for IIS
    /// </summary>
    public class SslCertificateProviderService
    {
        /// <summary>
        /// The logging service
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Shared storage path. The path where shared acme sites are deployed.
        /// </summary>
        protected string StoragePath;

        /// <summary>
        /// Application pool utils
        /// </summary>
        protected UtilsAppPool AppPoolUtils;

        /// <summary>
        /// 
        /// </summary>
        protected CmdRenewSslArgs EnvironmentSettings;

        /// <summary>
        /// Get an instance of SslCertificateProviderService
        /// </summary>
        public SslCertificateProviderService(
            ILoggerInterface logger,
            CmdRenewSslArgs settings)
        {
            this.AppPoolUtils = new UtilsAppPool(logger);

            // Everything performed against the staging API
            this.StoragePath = Path.Combine(
                AcmeChallengeSiteSetup.GetAcmeCentralSiteRoot(),
                "_provider_" + settings.CertProvider);

            UtilsSystem.EnsureDirectoryExists(this.StoragePath, true);

            this.EnvironmentSettings = settings;

            this.Logger = logger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        protected string FilePathForRenewalState(string hostName)
        {
            string key = hostName.Replace(".", "_");

            string path = Path.Combine(
                AcmeChallengeSiteSetup.GetAcmeCentralSiteRoot(),
                "_state_" + this.EnvironmentSettings.CertProvider,
                key + ".json");

            return path;
        }

        /// <summary>
        /// We store a target next renewal attempt, to avoid hitting let's encrypt api rate limits.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="errorStaleAfterHours">Consider error to be stale after 72 hours</param>
        protected void ThrowIfCertRenewalIsInErrorState(string hostName, int errorStaleAfterHours = 72)
        {
            var lastErrorPath = this.FilePathForRenewalState(hostName);

            if (!File.Exists(lastErrorPath))
            {
                return;
            }

            var contents = Newtonsoft.Json.JsonConvert.DeserializeObject<CertificateRenewalState>(File.ReadAllText(lastErrorPath));

            var hoursSinceLastError = (DateTime.UtcNow - contents.LastError).Hours;
            var remainingLockHours = errorStaleAfterHours - hoursSinceLastError;

            if (remainingLockHours <= 0)
            {
                File.Delete(lastErrorPath);
                return;
            }

            throw new RenewalCannotBeDoneException($"Certificate renewal for host {hostName} is locked for the next {remainingLockHours} hours with error message {contents.LastErrorDetails}. To remove the lock delete {lastErrorPath}.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="hostName"></param>
        protected void StoreCertificateRenewalState(CertificateRenewalState state, string hostName)
        {
            string path = this.FilePathForRenewalState(hostName);
            UtilsSystem.EnsureDirectoryExists(path);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(state));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected IAcmeSharpProvider GetProvider(string hostName)
        {
            IAcmeSharpProvider provider;

            switch (this.EnvironmentSettings.CertProvider)
            {
                case CertProvider.SelfSigned:
                    provider = new AcmeSharpProviderMock(this.Logger, hostName);
                    break;
                case CertProvider.Acme:
                    provider = new AcmeSharpProviderCertes(this.Logger, hostName, this.EnvironmentSettings);
                    break;
                case CertProvider.AcmeStaging:
                    provider = new AcmeSharpProviderCertes(this.Logger, hostName, this.EnvironmentSettings);
                    break;
                default:
                    throw new BusinessRuleException($"Cert provider {this.EnvironmentSettings.CertProvider} not supported.");
            }

            return provider;
        }

        /// <summary>
        /// Provisions a certificate in the central store
        /// </summary>
        /// <param name="hostName">Domain to register</param>
        /// <param name="email">Registration e-mail</param>
        /// <param name="force">Force renewal, even if renewal conditions are not met</param>
        /// <param name="useSelfSignedFallbackIfNoPfx">If provisioning fails and no certificate exists, a self sigend one is generated.</param>
        /// <returns>The certificate's friendly name, ready to be bound in IIS</returns>
        public void ProvisionCertificateInIis(
            string hostName,
            string email,
            bool force = false,
            bool useSelfSignedFallbackIfNoPfx = false)
        {
            if (!HostnameValidator.IsValidHostname(hostName))
            {
                throw new BusinessRuleException($"Malformed or invalid host name '{hostName}'.");
            }

            if (!this.EnvironmentSettings.DisableDnsValidation)
            {
                if (!UtilsIis.CheckHostnameDNS(hostName))
                {
                    // Throw only if we are not forcing and provider is not self-signed
                    if (!force && this.EnvironmentSettings.CertProvider != CertProvider.SelfSigned)
                    {
                        throw new BusinessRuleException($"DNS could not be retrieved for hostname {hostName}. Use -DisableDnsValidation to skip DNS validation.");
                    }

                    this.Logger.LogWarning(false, $"DNS could not be retrieved for hostname {hostName}");
                }
            }

            var currentCertificate = UtilsIis.FindCertificateInCentralCertificateStore(
                hostName,
                this.Logger,
                (new ApplicationDataStore().AppSettings.PfxPassword) ?? string.Empty,
                out var existingCertificateFilePath);

            if (currentCertificate == null && !string.IsNullOrWhiteSpace(existingCertificateFilePath))
            {
                string message =
                    $"A certificate file was found for hostname {hostName} but it's contents could not be read. See log for more details.";

                if (force)
                {
                    this.Logger.LogWarning(false, message);
                }
                else
                {
                    throw new BusinessRuleException(message);
                }
            }

            double remainingCertificateDays = (currentCertificate?.NotAfter - DateTime.Now)?.TotalDays ?? 0;
            this.Logger.LogInfo(true, "Total days remaining for certificate expiration: '{0}'", (int)Math.Floor(remainingCertificateDays));

            // Trigger renovation. Do this differently on mock/prod environment.
            // Next renewal attempt is calculated based on previous renewal attempt
            if (!force)
            {
                this.ThrowIfCertRenewalIsInErrorState(hostName);
            }

            // Si no hemos consumido más del 50% de la duración total del certificado, NO renovamos (excepto si uso FORCE)
            if (!force && remainingCertificateDays > this.EnvironmentSettings.RenewThresholdDays)
            {
                this.Logger.LogInfo(true, $"Certificate for '{hostName}' has has not reached renewal threshold of {this.EnvironmentSettings.RenewThresholdDays} days.'");
                return;
            }

            this.Logger.LogInfo(false, $"Attempting SSL certificate renewal for host '{hostName}' with provider '{this.EnvironmentSettings.CertProvider}'");

            // This is the final destination where the certificate will be writen to
            string certificateFilePath =
                Path.Combine(UtilsIis.GetCentralCertificateStorePath(this.Logger), hostName + ".pfx");

            // Do not even attempt renewal if we cannot write to destination folder
            if (!UtilsSystem.TestCanWriteFile(certificateFilePath, this.Logger))
            {
                throw new BusinessRuleException($"Cannot write certificate file {certificateFilePath}");
            }

            string acmeSharedStore = AcmeChallengeSiteSetup.GetAcmeCentralSiteRoot();

            IAcmeSharpProvider provider = this.GetProvider(hostName);

            bool useTemporaryBinding = false;

            string testChallengePath = null;

            string certificateLockFilePath = certificateFilePath + ".lock";

            if (File.Exists(certificateLockFilePath))
            {
                if (long.TryParse(File.ReadAllText(certificateLockFilePath), out long lockCreatedAt)
                    && (DateTime.UtcNow.ToUnixTimestamp() - lockCreatedAt) < 300)
                {
                    throw new RenewalCannotBeDoneException($"Certificate renewal lock in place at {certificateLockFilePath}");
                }
            }

            // It's true this is not 100% sync proof, but will suffice for our use case
            File.WriteAllText(certificateLockFilePath, ((long)DateTime.UtcNow.ToUnixTimestamp()).ToString());

            try
            {
                // Check that at least on site exists on this server with an available binding that can be used to provision
                // the certificate
                var bindings = UtilsIis.ParseSiteBindings(this.Logger, regexHost: $"^{Regex.Escape(hostName)}$|^$");

                // At least one binding should be using the hostname on port 80, and the site containing it must be started (including it's pool)
                var bindingForChallenge = bindings.Where((i) => (i.Binding.Binding.EndsWith($":80:{hostName}")
                                                                 || i.Binding.Binding.EndsWith($"*:80:")
                                                                 || i.Binding.Binding.EndsWith($"*:80:*"))
                                                                && i.Sites.Any((j) => j.State == ObjectState.Started))
                    .ToList();

                if (!bindingForChallenge.Any())
                {
                    this.Logger.LogWarning(false, "No HTTP binding found for hostname '" + hostName + "'. A temporary binding will be set up. If this hostname is load balanced, this binding will not be automatically added to the other nodes and might fail. A simple workaround is to add a catch-all http binding on all load balanced server.");
                    useTemporaryBinding = true;
                    UtilsIis.AddHttpBindingToSite(AcmeChallengeSiteSetup.AcmeChallengeSiteName, "*:80:" + hostName.ToLower(), "http");
                }

                // Just check that the local resolver does WORK before attempting anything remotely
                var testChallengeFilename =
                    "_" + DateTime.UtcNow.ToString("yyyyMMdd") + "_test_" + Guid.NewGuid().ToString();
                var testChallengeContents = Guid.NewGuid().ToString();

                testChallengePath = Path.Combine(acmeSharedStore, hostName, ".well-known\\acme-challenge", testChallengeFilename);
                UtilsSystem.EnsureDirectoryExists(testChallengePath);

                var testChallengeUri =
                    $"http://localhost:8095/{hostName}/.well-known/acme-challenge/{testChallengeFilename}";
                var testChallengeUri2 = $"http://{hostName}/.well-known/acme-challenge/{testChallengeFilename}";

                File.WriteAllText(testChallengePath, testChallengeContents);

                this.Logger.LogInfo(true, "Writing local test challenge to: '{0}'", testChallengePath);

                string remoteData = null;

                try
                {
                    this.Logger.LogInfo(false, "Validating local test challenge setup at: '{0}'", testChallengeUri);

                    remoteData =
                        UtilsSystem.RetryWhile(
                            () =>
                                UtilsSystem.DownloadUriAsText(
                                    testChallengeUri,

                                    // Make sure we use loopback address, as this is just a local test.
                                    forceIpAddress: "127.0.0.1"),
                            (e) => true,
                            3000,
                            this.Logger,
                            2);

                    this.Logger.LogInfo(false, $"Challenge value retrieved: " + remoteData);

                    this.Logger.LogInfo(false, "Validating local test challenge setup at: '{0}'", testChallengeUri2);

                    remoteData =
                        UtilsSystem.RetryWhile(
                            () => UtilsSystem.DownloadUriAsText(
                                testChallengeUri2,

                                // Make sure we use loopback address, as this is just a local test.
                                forceIpAddress: "127.0.0.1"),
                            (e) => true,
                            3000,
                            this.Logger,
                            2);

                    this.Logger.LogInfo(false, $"Challenge value retrieved: " + remoteData);
                }
                catch (CustomWebException e)
                {
                    this.Logger.LogWarning(
                        false,
                        $"Local server request to {testChallengeUri} (looped to 127.0.0.1) returned an invalid response and certificate provisioning was skipped: " +
                        e.Contents);

                    this.HoldStateIfInConsole();

                    ExceptionDispatchInfo.Capture(e).Throw();
                }
                finally
                {
                    File.Delete(testChallengePath);
                }

                if (!string.Equals(remoteData, testChallengeContents))
                {
                    throw new BusinessRuleException(
                        $"Could not locally validate acme challenge site setup at {testChallengeUri}, retrieved challenge value {remoteData} does not match challenge {testChallengeContents}");
                }

                // Ssl registration configuration only depends on the e-mail and is signed as such
                string sslSignerAndRegistrationStoragePath = UtilsSystem.EnsureDirectoryExists(
                    UtilsSystem.CombinePaths(
                        this.StoragePath,
                        "_ssl_config",
                        email.Replace("@", "_at_")),
                    true);

                GlobalCancellationTokenManager.CancellationToken?.ThrowIfCancellationRequested();

                provider.InitRegistration(sslSignerAndRegistrationStoragePath, email);

                provider.GenerateHttpChallenge(
                    out var challengeUrl,
                    out var challengeContent,
                    out var challengeFilePath);

                if (!challengeFilePath.StartsWith(".well-known/acme-challenge/", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new BusinessRuleException(
                        "Only validation requests that start with .well-known are supported, received: " +
                        challengeFilePath);
                }

                var challengeValidated = false;
                string challengeFullPath = null;

                Exception challengeValidationException = null;

                if (challengeUrl != null)
                {
                    // Write the challanege contents
                    challengeFullPath =
                        Path.Combine(acmeSharedStore, hostName, challengeFilePath);

                    UtilsSystem.EnsureDirectoryExists(challengeFullPath);

                    File.WriteAllText(
                        challengeFullPath,
                        challengeContent);

                    this.Logger.LogInfo(true, "Written challenge file to: " + challengeFullPath);

                    this.Logger.LogInfo(false, $"Verifying challenge at '{challengeUrl}'");

                    try
                    {
                        // Validate that we can actually access the challenge ourselves, this is actually not reliable, just a test. It's not reliable
                        // because we are NOT looping to internal IP so many things can go "wrong" such as networking.
                        if (!(provider is AcmeSharpProviderMock))
                        {
                            string contents =
                                UtilsSystem.RetryWhile(
                                    () => UtilsSystem.DownloadUriAsText(challengeUrl),
                                    (e) => true,
                                    8000,
                                    this.Logger,
                                    2);

                            if (!string.Equals(contents, challengeContent))
                            {
                                throw new BusinessRuleException(
                                    $"Could not validate ACME challenge, retrieved challenge '{contents}' does not match '{challengeContent}'");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.Logger.LogInfo(
                            false,
                            "Cannot self-verify auth challenge, this can sometimes happen under some DNS setups or networking, and is not necessarily a bad thing. {0}",
                            e.Message + Environment.NewLine + e.InnerException?.Message);
                    }

                    try
                    {
                        challengeValidated = provider.ValidateChallenge();

                        // This is here for testing purposes
                        if (UnitTestDetector.IsRunningInTests &&
                            !string.IsNullOrWhiteSpace(
                                Environment.GetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION")))
                        {
                            challengeValidated = false;
                            throw new Exception("Remote challenge validation failed TEST_FAIL_CHALLENGE_VALIDATION");
                        }
                    }
                    catch (Exception e)
                    {
                        challengeValidationException = e;
                    }

                    if (!challengeValidated)
                    {
                        this.Logger.LogError(
                            "Challenge could not be validated at '{0}'. If behind a load balancer, make sure that the site is deployed in ALL nodes, remove the self-signed certificate from the store and redeploy the application.",
                            challengeUrl);

                        this.HoldStateIfInConsole();
                    }

                    this.Logger.LogInfo(
                        true,
                        "Remote challenge validation success: " + (challengeValidated ? "Yes" : "No"));
                }

                // Download the certificates to this temp location
                string temporaryCertificatePath = UtilsSystem.EnsureDirectoryExists(
                    UtilsSystem.CombinePaths(
                        Path.GetTempPath(),
                        "ssl_certificates",
                        hostName),
                    true);

                CertificatePaths certificatepaths = null;

                if (challengeValidated)
                {
                    try
                    {
                        certificatepaths = provider.DownloadCertificate(
                            UtilsEncryption.GetMD5(hostName),
                            hostName,
                            temporaryCertificatePath,
                            (new ApplicationDataStore().AppSettings.PfxPassword) ?? string.Empty);
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

                if (certificatepaths == null && currentCertificate == null && useSelfSignedFallbackIfNoPfx)
                {
                    this.Logger.LogWarning(
                        false,
                        "Unable to acquire certificate and site does not have a valid existing one, using self-signed fallback.");

                    provider = new AcmeSharpProviderMock(this.Logger, hostName);

                    certificatepaths = provider.DownloadCertificate(
                        UtilsEncryption.GetMD5(hostName),
                        hostName,
                        temporaryCertificatePath,
                        (new ApplicationDataStore().AppSettings.PfxPassword) ?? string.Empty);
                }

                // Save this, use a fixed name certificate file
                if (certificatepaths != null)
                {
                    UtilsSystem.RetryWhile(
                        () =>
                        {
                            File.Copy(certificatepaths.pfxPemFile, certificateFilePath, true);
                            return true;
                        },
                        (e) => true,
                        2500,
                        this.Logger);

                    this.Logger.LogInfo(false, "Certificate file writen to '{0}'", certificateFilePath);
                }

                // Remove temporary certificates
                UtilsSystem.DeleteDirectory(temporaryCertificatePath, this.Logger);

                // Remove the already used challenge if it was validated. Otherwise keep it
                // for debugging purposes.
                if (challengeValidated && challengeFullPath != null && File.Exists(challengeFullPath))
                {
                    File.Delete(challengeFullPath);
                }

                if (!challengeValidated && challengeValidationException != null)
                {
                    ExceptionDispatchInfo.Capture(challengeValidationException).Throw();
                }
            }
            catch (RenewalCannotBeDoneException renewalCannotBeDoneException)
            {
                // We don't really want to do anything.
                ExceptionDispatchInfo.Capture(renewalCannotBeDoneException).Throw();
            }
            catch (Exception e)
            {
                if (e is AggregateException aggregateException && aggregateException.InnerExceptions?.Count == 1)
                {
                    e = aggregateException.InnerExceptions.First();
                }

                this.StoreCertificateRenewalState(
                    new CertificateRenewalState()
                    {
                        LastErrorDetails = e.Message,
                        LastError = DateTime.UtcNow
                    },
                    hostName);

                this.Logger.LogError(
                    $"Unhandled exception '{e.Message}' when renewing certificate. Temporarily disabling automatic renewal for host {hostName}");

                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                File.Delete(certificateLockFilePath);

                if (useTemporaryBinding)
                {
                    UtilsIis.RemoveHttpBindingFromSite(AcmeChallengeSiteSetup.AcmeChallengeSiteName, "*:80:" + hostName.ToLower(), "http");
                }

                if (!string.IsNullOrWhiteSpace(testChallengePath) && File.Exists(testChallengePath))
                {
                    File.Delete(testChallengePath);
                }

                provider?.Dispose();
            }
        }

        /// <summary>
        /// Holds the console when in userinteractive mode
        /// </summary>
        protected void HoldStateIfInConsole()
        {
            // If this is a console... then hold on to the deployment for some time...
            if (Environment.UserInteractive && !UnitTestDetector.IsRunningInTests)
            {
                try
                {
                    this.Logger.LogWarning(
                        true,
                        "The current deployment will be held press Enter to continue...");

                    Reader.ReadLine(30 * 60 * 1000);
                }
                catch (TimeoutException)
                {
                }
            }
        }
    }
}
