using ACMESharp;
using ACMESharp.ACME;
using ACMESharp.JOSE;
using ACMESharp.PKI;
using ACMESharp.PKI.RSA;
using ACMESharp.POSH.Util;
using ACMESharp.Vault;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace iischef.core.IIS.AcmeProviders
{
    public class AcmeSharpProvider : IAcmeSharpProvider
    {
        /// <summary>
        /// 
        /// </summary>
        protected const string AcmeStaging = "https://acme-staging.api.letsencrypt.org/";

        /// <summary>
        /// 
        /// </summary>
        protected const string AcmeLive = "https://acme-v01.api.letsencrypt.org/";

        /// <summary>
        /// The storage vault
        /// </summary>
        private readonly IVault Vault;

        /// <summary>
        /// 
        /// </summary>
        private RS256Signer Signer;

        /// <summary>
        /// 
        /// </summary>
        private AcmeClient AcmeClient;

        /// <summary>
        /// 
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        protected EnvironmentSettings Settings;

        /// <summary>
        /// The domain to auth
        /// </summary>
        protected string Domain;

        /// <summary>
        /// 
        /// </summary>
        protected AuthorizationState authState;

        /// <summary>
        /// 
        /// </summary>
        protected AuthorizeChallenge Challenge;

        /// <summary>
        /// 
        /// </summary>
        public AcmeSharpProvider(ILoggerInterface logger, string domain, EnvironmentSettings settings)
        {
            this.Settings = settings;
            this.Logger = logger;
            this.Domain = domain;

            this.Logger.LogInfo(true, "Using ACME SHARP uri {0}", this.AcmeUri);

            this.Vault = VaultHelper.GetVault(":user");

            if (!this.Vault.TestStorage())
            {
                try
                {
                    this.Vault.InitStorage();
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Actual acme uri to use, can be configured through global settigs
        /// </summary>
        protected string AcmeUri
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.Settings.AcmeUri))
                {
                    return this.Settings.AcmeUri;
                }

                return AcmeLive;
            }
        }

        [Obsolete("Only use this for testing purposes.")]
        public static void TestVault()
        {
            var vault = VaultHelper.GetVault(":user");

            if (!vault.TestStorage())
            {
                try
                {
                    vault.InitStorage();
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public void InitRegistration(
            string signerPath,
            string registrationPath,
            string email)
        {
            this.Signer = new RS256Signer();
            this.Signer.Init();

            string signerBackupPath = signerPath + ".bak";
            string registrationBackupPath = registrationPath + ".bak";

            // For some unkown reason, signer and registration files get corrupted at 0 bytes who knows
            // why during usage, so we keep backups that are restored when corruption is detected.
            // Probably due to a buggy implementation downwards in this same method...

            if (File.Exists(signerBackupPath) && File.Exists(signerPath) && new FileInfo(signerPath).Length == 0)
            {
                this.Logger.LogWarning(true, "Signer file corrupted, restoring from backup: {0}", signerBackupPath);
                File.Copy(signerBackupPath, signerPath, true);
            }

            if (File.Exists(registrationBackupPath) && File.Exists(registrationPath) && new FileInfo(registrationPath).Length == 0)
            {
                this.Logger.LogWarning(true, "Registration file corrupted, restoring from backup: {0}", registrationBackupPath);
                File.Copy(registrationBackupPath, registrationPath, true);
            }

            // Load the signer

            if (File.Exists(signerPath))
            {
                try
                {
                    this.LoadSignerFromFile(this.Signer, signerPath);
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not load signer from path: {signerPath}", e);
                }
            }

            this.AcmeClient = new AcmeClient(new Uri(this.AcmeUri), new AcmeServerDirectory(), signer: this.Signer);

            this.AcmeClient.Init();
            this.AcmeClient.GetDirectory(true);

            if (File.Exists(registrationPath))
            {
                try
                {
                    using (var registrationStream = File.OpenRead(registrationPath))
                    {
                        this.AcmeClient.Registration = AcmeRegistration.Load(registrationStream);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not load registration from path: {registrationPath}", e);
                }
            }
            else
            {
                this.Logger.LogInfo(true, "Calling Register");

                AcmeRegistration registration = this.AcmeClient.Register(new string[] { "mailto:" + email });

                this.Logger.LogInfo(true, "Updating Registration");

                this.AcmeClient.UpdateRegistration(useRootUrl: true, agreeToTos: true);

                this.Logger.LogInfo(true, "Saving Registration");

                using (var registrationStream = File.OpenWrite(registrationPath))
                {
                    this.AcmeClient.Registration.Save(registrationStream);
                }

                File.Copy(registrationPath, registrationBackupPath);

                this.Logger.LogInfo(true, "Saving Signer");

                using (var signerStream = File.OpenWrite(signerPath))
                {
                    this.Signer.Save(signerStream);
                }

                File.Copy(signerPath, signerBackupPath);
            }
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public void GenerateHttpChallenge(out string challengeUrl, out string challengeContent, out string challengeFilePath)
        {
            this.authState = this.AcmeClient.AuthorizeIdentifier(this.Domain);
            this.Challenge = this.AcmeClient.DecodeChallenge(this.authState, AcmeProtocol.CHALLENGE_TYPE_HTTP);
            var httpChallenge = (HttpChallenge)this.Challenge.Challenge;
            challengeUrl = httpChallenge.FileUrl;
            challengeContent = httpChallenge.FileContent;
            challengeFilePath = httpChallenge.FilePath;
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public bool ValidateChallenge()
        {
            // (6) Submit the Challenge Response to Prove Domain Ownership
            this.authState.Challenges = new AuthorizeChallenge[] { this.Challenge };

            // Submit-ACMEChallenge dns1 -ChallengeType http-01
            var authchallenge = this.AcmeClient.SubmitChallengeAnswer(this.authState, AcmeProtocol.CHALLENGE_TYPE_HTTP, true);

            // Esperar a que Let's Encrypt confirme que has superado el challange
            while ("pending".Equals(this.authState.Status, StringComparison.CurrentCultureIgnoreCase))
            {
                Thread.Sleep(3000);

                var newAuthzState = this.AcmeClient.RefreshIdentifierAuthorization(this.authState);

                if (newAuthzState.Status != "pending")
                {
                    this.authState = newAuthzState;
                }
            }

            if (this.authState.Status != "valid")
            {
                this.Logger.LogWarning(
                    true,
                    "Could not verify challenge valid status, returned status: '{0}'",
                    this.authState.Status);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Generate a certificate request
        /// </summary>
        /// <param name="certificatename"></param>
        /// <param name="mainhost">Main host: www.google.com</param>
        /// <param name="certificatePath">Path to store the generated certificates</param>
        /// <param name="alternatehosts">Alterante hosts: list of alterante hosts for this certificate</param>
        /// <returns></returns>
        public CertificatePaths DownloadCertificate(
            string certificatename,
            string mainhost,
            string certificatePath,
            List<string> alternatehosts = null)
        {
            if (alternatehosts != null && alternatehosts.Any())
            {
                throw new NotSupportedException("Alternate host provisioning not supported yet.");
            }

            List<string> allDnsIdentifiers = new List<string>() { mainhost };

            if (alternatehosts != null)
            {
                allDnsIdentifiers.AddRange(alternatehosts);
            }

            // Tomado de app.config
            var rsaKeyBits = 2048; // 1024;//

            if (Environment.Is64BitProcess)
            {
                CertificateProvider.RegisterProvider(typeof(ACMESharp.PKI.Providers.OpenSslLib64Provider));
            }
            else
            {
                CertificateProvider.RegisterProvider(typeof(ACMESharp.PKI.Providers.OpenSslLib32Provider));
            }

            var cp = CertificateProvider.GetProvider();
            var rsaPkp = new RsaPrivateKeyParams();
            try
            {
                if (rsaKeyBits >= 1024)
                {
                    rsaPkp.NumBits = rsaKeyBits;

                    // Log.Debug("RSAKeyBits: {RSAKeyBits}", Properties.Settings.Default.RSAKeyBits);
                }
                else
                {
                    // Log.Warning(
                    //    "RSA Key Bits less than 1024 is not secure. Letting ACMESharp default key bits. http://openssl.org/docs/manmaster/crypto/RSA_generate_key_ex.html");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogInfo(true, $"Unable to set RSA Key Bits, Letting ACMESharp default key bits, Error: {ex.Message.ToString()}");
            }

            var rsaKeys = cp.GeneratePrivateKey(rsaPkp);
            var csrDetails = new CsrDetails
            {
                CommonName = allDnsIdentifiers[0]
            };

            if (alternatehosts != null)
            {
                if (alternatehosts.Count > 0)
                {
                    csrDetails.AlternativeNames = alternatehosts;
                }
            }

            var csrParams = new CsrParams
            {
                Details = csrDetails,
            };

            var csr = cp.GenerateCsr(csrParams, rsaKeys, Crt.MessageDigest.SHA256);

            byte[] derRaw;
            using (var bs = new MemoryStream())
            {
                cp.ExportCsr(csr, EncodingFormat.DER, bs);
                derRaw = bs.ToArray();
            }

            var derB64U = JwsHelper.Base64UrlEncode(derRaw);

            this.Logger.LogInfo(true, $"Requesting Certificate");

            // Log.Information("Requesting Certificate");
            var certRequ = this.AcmeClient.RequestCertificate(derB64U);

            // Log.Debug("certRequ {@certRequ}", certRequ);

            this.Logger.LogInfo(true, $"Request Status: {certRequ.StatusCode}");

            if (certRequ.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception($"Could not create certificate request, response code: = {certRequ.StatusCode}");
            }

            var keyGenFile = Path.Combine(certificatePath, $"{certificatename}-gen-key.json");
            var keyPemFile = Path.Combine(certificatePath, $"{certificatename}-key.pem");
            var csrGenFile = Path.Combine(certificatePath, $"{certificatename}-gen-csr.json");
            var csrPemFile = Path.Combine(certificatePath, $"{certificatename}-csr.pem");
            var crtDerFile = Path.Combine(certificatePath, $"{certificatename}-crt.der");
            var crtPemFile = Path.Combine(certificatePath, $"{certificatename}-crt.pem");
            var chainPemFile = Path.Combine(certificatePath, $"{certificatename}-chain.pem");
            var pfxPemFile = Path.Combine(certificatePath, $"{certificatename}.pfx");

            using (var fs = new FileStream(keyGenFile, FileMode.Create))
            {
                cp.SavePrivateKey(rsaKeys, fs);
            }

            using (var fs = new FileStream(keyPemFile, FileMode.Create))
            {
                cp.ExportPrivateKey(rsaKeys, EncodingFormat.PEM, fs);
            }

            using (var fs = new FileStream(csrGenFile, FileMode.Create))
            {
                cp.SaveCsr(csr, fs);
            }

            using (var fs = new FileStream(csrPemFile, FileMode.Create))
            {
                cp.ExportCsr(csr, EncodingFormat.PEM, fs);
            }

            this.Logger.LogInfo(true, $"Saving Certificate to {crtDerFile}");

            // Log.Information("Saving Certificate to {crtDerFile}", crtDerFile);
            using (var file = File.Create(crtDerFile))
            {
                certRequ.SaveCertificate(file);
            }

            Crt crt;

            using (FileStream source = new FileStream(crtDerFile, FileMode.Open),
                target = new FileStream(crtPemFile, FileMode.Create))
            {
                crt = cp.ImportCertificate(EncodingFormat.DER, source);
                cp.ExportCertificate(crt, EncodingFormat.PEM, target);
            }

            cp.Dispose();

            var ret = new CertificatePaths()
            {
                chainPemFile = chainPemFile,
                crtDerFile = crtDerFile,
                crtPemFile = crtPemFile,
                csrGenFile = csrGenFile,
                csrPemFile = csrPemFile,
                keyGenFile = keyGenFile,
                keyPemFile = keyPemFile,
                name = certificatename,
                pfxPemFile = pfxPemFile
            };

            // Generate the PFX version manually
            UtilsCertificate.CreatePfXfromPem(ret.crtPemFile, ret.keyPemFile, ret.pfxPemFile, null);

            return ret;
        }

        /// <summary>
        /// Load a certificate signer from a file
        /// </summary>
        /// <param name="signer"></param>
        /// <param name="signerPath"></param>
        protected void LoadSignerFromFile(RS256Signer signer, string signerPath)
        {
            this.Logger.LogInfo(true, $"Loading Signer from {signerPath}");
            using (var signerStream = File.OpenRead(signerPath))
            {
                signer.Load(signerStream);
            }
        }

        public void Dispose()
        {
            this.Signer?.Dispose();
            this.AcmeClient?.Dispose();
        }
    }
}
