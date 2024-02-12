using iischef.logger;
using iischef.utils;
using System;
using System.Collections.Generic;
using System.IO;

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
            string configurationStorePath,
            string email)
        {
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
            return true;
        }

        /// <inheritdoc cref="IAcmeSharpProvider"/>
        public CertificatePaths DownloadCertificate(
            string certificateFriendlyName,
            string hostName,
            string certificatePath,
            string password)
        {
            var result = new CertificatePaths();

            // Creating the self-signed certificate already enrolls it in the local certificate store
            this.Logger.LogInfo(true, "Generating self signed certificate.");

            var tmpPfx = Path.GetTempFileName();
            UtilsCertificate.CreateSelfSignedCertificateAsPfx(this.Domain, tmpPfx, password, null, this.Logger, 90);
            result.pfxPemFile = tmpPfx;
            return result;
        }

        public void Dispose()
        {
            this.Logger = null;
        }
    }
}
