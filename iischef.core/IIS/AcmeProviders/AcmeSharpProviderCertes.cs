using Certify.ACME.Anvil;
using Certify.ACME.Anvil.Acme;
using Certify.ACME.Anvil.Acme.Resource;
using iischef.logger;
using iischef.utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Directory = Certify.ACME.Anvil.Acme.Resource.Directory;

namespace iischef.core.IIS.AcmeProviders
{
    /// <summary>
    /// ACME challenge validator using the CERTES library
    /// </summary>
    public class AcmeSharpProviderCertes : IAcmeSharpProvider
    {
        /// <summary>
        /// 
        /// </summary>
        protected readonly ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        protected readonly string HostName;

        /// <summary>
        /// 
        /// </summary>
        protected IOrderContext OrderContext = null;

        /// <summary>
        /// 
        /// </summary>
        protected HttpClient HttpClient = null;

        /// <summary>
        /// The http challenge
        /// </summary>
        protected IChallengeContext HttpChallenge;

        /// <summary>
        /// 
        /// </summary>
        protected CmdRenewSslArgs Settings;

        /// <summary>
        /// 
        /// </summary>
        public AcmeContext AcmeContext { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public AcmeHttpClient AcmeHttpClient { get; private set; }

        /// <summary>
        /// Account Certes
        /// </summary>
        public CertesSettings CertesSettings { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="hostName"></param>
        /// <param name="settings"></param>
        public AcmeSharpProviderCertes(ILoggerInterface logger, string hostName, CmdRenewSslArgs settings)
        {
            this.Settings = settings;
            this.Logger = logger;
            this.HostName = hostName;
            this.HttpClient = new HttpClient();
        }

        /// <summary>
        /// Actual acme uri to use, can be configured through global settigs
        /// </summary>
        protected string AcmeUri
        {
            get
            {
                switch (this.Settings.CertProvider)
                {
                    case CertProvider.Acme:
                        return WellKnownServers.LetsEncryptV2.AbsoluteUri;
                    case CertProvider.AcmeStaging:
                        return WellKnownServers.LetsEncryptStagingV2.AbsoluteUri;
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Register a new order on the ACME service, for the specified domains. Challenges will be automatically verified.
        /// This method manages automatically the creation of necessary directory and files.
        /// </summary>
        /// <remarks>
        /// When using HTTP Validation, the ACME directory will access to http://__domain__/.well-known/acme-challenge/token, that should be served 
        /// by a local web server when not using built-in, and translated into local path {challengeVerifyPath}\.well-known\acme-challenge\token.
        /// Important Note: currently WinCertes supports only http-01 validation mode, and dns-01 validation mode with limitations.
        /// </remarks>
        /// <returns></returns>
        public bool ValidateChallenge()
        {
            if (this.HttpChallenge == null)
            {
                this.Logger.LogError("HTTP Challenge Validation set up, but server sent no HTTP Challenge");
                return false;
            }

            this.Logger.LogInfo(true, $"Initiating HTTP Validation");

            bool resValidation = false;

            UtilsSystem.RetryWhile(
                () =>
                {
                    resValidation = this.ValidateHTTPChallenge(this.HttpChallenge).Result;
                    return true;
                },
                (e) => e is IOException || e is WebException || e is SocketException,
                12000,
                this.Logger);

            if (!resValidation)
            {
                this.Logger.LogError($"Could not validate HTTP challenge:\n {this.HttpChallenge.Resource().Result.Error.Detail}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generate a certificate request
        /// </summary>
        /// <param name="certificateFriendlyName"></param>
        /// <param name="hostName">Main host: www.google.com</param>
        /// <param name="certificatePath">Path to store the generated certificates</param>
        /// <param name="password"></param>
        /// <returns></returns>
        public CertificatePaths DownloadCertificate(
            string certificateFriendlyName,
            string hostName,
            string certificatePath,
            string password)
        {
            if (this.OrderContext == null)
            {
                throw new BusinessRuleException("Do not call RetrieveCertificate before RegisterNewOrderAndVerify");
            }

            // Let's generate a new key (RSA is good enough IMHO)
            IKey privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);

            // Then let's generate the CSR
            var csr = this.OrderContext.CreateCsr(privateKey).Result;
            csr.AddName("CN", hostName);

            // and finalize the ACME order
            var finalOrder = this.OrderContext.Finalize(csr.Generate()).Result;

            // Now we can fetch the certificate
            CertificateChain cert = this.OrderContext.Download().Result;

            // We build the PFX/PKCS#12 and the cert/key as PEM
            var pfx = cert.ToPfx(privateKey);
            var cer = cert.ToPem();
            var key = privateKey.ToPem();

            // pfx.AddIssuers(this.GetCACertChainFromStore()); Remote certificate already has information
            var fileName = Path.Combine(certificatePath, Guid.NewGuid().ToString());
            var pfxName = fileName + ".pfx";
            var cerPath = fileName + ".cer";
            var keyPath = fileName + ".key";

            var authenticatedPfx = new AuthenticatedPFX(pfxName, cerPath, keyPath, password);
            var pfxBytes = pfx.Build(certificateFriendlyName, authenticatedPfx.PfxPassword, allowBuildWithoutKnownRoot: true);

            // We write the PFX/PKCS#12 to file
            File.WriteAllBytes(pfxName, pfxBytes);
            this.Logger.LogInfo(true, $"Retrieved certificate from the CA. The certificate is in '{pfxName}'.");

            // We write the PEMs to corresponding files
            File.WriteAllText(cerPath, cer);
            File.WriteAllText(keyPath, key);

            return new CertificatePaths()
            {
                chainPemFile = string.Empty,
                crtDerFile = string.Empty,
                crtPemFile = cerPath,
                csrGenFile = string.Empty,
                csrPemFile = string.Empty,
                keyGenFile = string.Empty,
                keyPemFile = keyPath,
                name = certificateFriendlyName,
                pfxPemFile = pfxName
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="challengeUrl"></param>
        /// <param name="challengeContent"></param>
        /// <param name="challengeFilePath"></param>
        public void GenerateHttpChallenge(out string challengeUrl, out string challengeContent, out string challengeFilePath)
        {
            this.OrderContext = this.AcmeContext.NewOrder(new List<string>() { this.HostName }).Result;

            if (this.OrderContext == null)
            {
                throw new BusinessRuleException("Could not create certificate order.");
            }

            // And fetching authorizations
            var orderAuthz = this.OrderContext.Authorizations().Result;

            // Looping through authorizations
            foreach (IAuthorizationContext authz in orderAuthz)
            {
                // Although this is a loop, we only allow one authorization per request in our implementation,
                this.HttpChallenge = this.GenerateChallengeRequests(authz).Result;

                if (this.HttpChallenge == null)
                {
                    break;
                }

                // TODO: Revisar estos valores
                challengeContent = this.HttpChallenge.KeyAuthz;
                challengeUrl = $"http://{this.HostName}/.well-known/acme-challenge/" + this.HttpChallenge.Token;
                challengeFilePath = ".well-known/acme-challenge/" + this.HttpChallenge.Token;
                return;
            }

            throw new BusinessRuleException("No authorization order was created.");
        }

        /// <inheritdoc cref="IAcmeSharpProvider.InitRegistration"/>
        public void InitRegistration(
            string configurationStorePath,
            string email)
        {
            var registrationSubDir = UtilsSystem.EscapeStringForDirectoryName(email) + UtilsEncryption.GetShortHash(email + this.AcmeUri);
            string settingsFilePath = Path.Combine(configurationStorePath, registrationSubDir, "certes.json");

            UtilsSystem.EnsureDirectoryExists(settingsFilePath);

            // Initialization and renewal/revocation handling
            // We get the CertesWrapper object, that will do most of the job.
            // RS256 Let's generate a new key (RSA is good enough IMHO)
            var serviceUri = new Uri(this.AcmeUri);
            this.Logger.LogInfo(false, "Using Acme URI: " + serviceUri);

            CertesSettings settings;

            this.AcmeHttpClient = new AcmeHttpClient(serviceUri, this.HttpClient);

            if (File.Exists(settingsFilePath))
            {
                // Si ya teníamos unos settings, siginifica que la cuenta ya está registrada
                settings =
                    JsonConvert.DeserializeObject<CertesSettings>(File.ReadAllText(settingsFilePath));

                this.AcmeContext = new AcmeContext(serviceUri, KeyFactory.FromDer(settings.Key), this.AcmeHttpClient);
            }
            else
            {
                // Hay que crear una nueva cuenta con su clave, y registrarla en ACME
                settings = new CertesSettings()
                {
                    AccountEmail = email,
                    ServiceUri = serviceUri,
                    Key = KeyFactory.NewKey(KeyAlgorithm.RS256).ToDer()
                };

                // Register the account
                this.AcmeContext = new AcmeContext(serviceUri, KeyFactory.FromDer(settings.Key), this.AcmeHttpClient);
                IAccountContext accountCtx = this.AcmeContext.NewAccount(settings.AccountEmail, true).Result;
                File.WriteAllText(settingsFilePath, JsonConvert.SerializeObject(settings));

                Directory directory = this.AcmeContext.GetDirectory().Result;
                this.Logger.LogInfo(true, $"Successfully registered account {settings.AccountEmail} with certificate authority {serviceUri.AbsoluteUri}");

                if ((directory.Meta != null) && (directory.Meta.TermsOfService != null))
                {
                    this.Logger.LogInfo(true, $"Please check the ACME Service ToS at: {directory.Meta.TermsOfService}");
                }
            }

            this.CertesSettings = settings;
        }

        /// <summary>
        /// Dispose elements of AcmeSharpProviderV2
        /// </summary>
        public void Dispose()
        {
            this.OrderContext = null;
            this.HttpClient?.Dispose();
            this.CertesSettings = null;
            this.AcmeContext = null;
            this.AcmeHttpClient = null;
        }

        /// <summary>
        /// Validates an Authorization, switching between DNS and HTTP challenges
        /// </summary>
        /// <param name="authz"></param>
        /// <returns></returns>
        private async Task<IChallengeContext> GenerateChallengeRequests(IAuthorizationContext authz)
        {
            var allChallenges = await authz.Challenges();
            var res = await authz.Resource();

            // Get the HTTP challenge
            var httpChallenge = await authz.Http();

            // Store the challenge info for later validation
            return httpChallenge;
        }

        /// <summary>
        /// Small method that validates one challenge using the specified validator
        /// </summary>
        /// <param name="httpChallenge"></param>
        /// <returns>true if validated, false otherwise</returns>
        private async Task<bool> ValidateHTTPChallenge(IChallengeContext httpChallenge)
        {
            // We get the resource fresh
            var httpChallengeStatus = await httpChallenge.Resource();

            // If it's invalid, we stop right away. Should not happen, but anyway...
            if (httpChallengeStatus.Status == ChallengeStatus.Invalid)
            {
                throw new BusinessRuleException("HTTP challenge has an 'Invalid' status");
            }

            // Now let's ping the ACME service to validate the challenge token
            Challenge challengeRes = await httpChallenge.Validate();

            // We need to loop, because ACME service might need some time to validate the challenge token
            int retry = 0;

            while (((challengeRes.Status == ChallengeStatus.Pending) || (challengeRes.Status == ChallengeStatus.Processing)) && (retry < 8))
            {
                // We sleep 2 seconds between each request, to leave time to ACME service to refresh
                System.Threading.Thread.Sleep(1500);

                // We refresh the challenge object from ACME service
                challengeRes = await httpChallenge.Resource();
                retry++;
            }

            // If challenge is Invalid, Pending or Processing, something went wrong...
            if (challengeRes.Status != ChallengeStatus.Valid)
            {
                return false;
            }

            return true;
        }
    }
}