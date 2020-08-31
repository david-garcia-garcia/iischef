using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using iischef.logger;
using iischef.utils;

namespace iischef.core.IIS.AcmeProviders
{
    /// <summary>
    /// Mock provider for tests
    /// </summary>
    public class AcmeSharpProviderMock : IAcmeSharpProvider
    {
        protected string Domain;

        protected string ChallengeUrl;

        protected ILoggerInterface Logger;

        protected string ChallengeContent;

        public AcmeSharpProviderMock(ILoggerInterface logger, string domain)
        {
            this.Logger = logger;
            this.ChallengeContent = Guid.NewGuid().ToString();
            this.Domain = domain;
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public void InitRegistration(
            string signerPath,
            string registrationPath,
            string email)
        {
            File.WriteAllText(signerPath, string.Empty);
            File.WriteAllText(registrationPath, string.Empty);
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public void GenerateHttpChallenge(out string challengeUrl, out string challengeContent, out string challengeFilePath)
        {
            challengeFilePath = $".well-known/acme-challenge/" + Guid.NewGuid();
            challengeUrl = $"http://{this.Domain}/" + challengeFilePath;
            this.ChallengeUrl = challengeUrl;
            challengeContent = this.ChallengeContent;
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public bool ValidateChallenge()
        {
            return string.Equals(UtilsSystem.DownloadUriAsText(this.ChallengeUrl), this.ChallengeContent);
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public CertificatePaths DownloadCertificate(
            string certificatename,
            string mainhost,
            string certificatePath,
            List<string> alternatehosts = null)
        {
            var result = new CertificatePaths();

            // Creating the self-signed certificate already enrolls it in the local certificate store
            this.Logger.LogInfo(true, "Generating self signed certificate.");

            var tmpPfx = Path.GetTempFileName();
            UtilsCertificate.CreateSelfSignedCertificateAsPfx(this.Domain, tmpPfx, string.Empty, null, this.Logger, 90);
            result.pfxPemFile = tmpPfx;

            return result;
        }

        public void Dispose()
        {
            this.Logger = null;
        }
    }
}
