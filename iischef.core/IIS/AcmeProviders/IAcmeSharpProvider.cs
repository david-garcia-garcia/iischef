using iischef.utils;
using System;
using System.Collections.Generic;

namespace iischef.core.IIS
{
    public interface IAcmeSharpProvider : IDisposable
    {
        /// <summary>
        /// Initialize registration settings
        /// </summary>
        /// <param name="configurationStorePath"></param>
        /// <param name="email"></param>
        void InitRegistration(
            string configurationStorePath,
            string email);

        /// <summary>
        /// Generate a challenge
        /// </summary>
        /// <param name="challengeUrl"></param>
        /// <param name="challengeContent"></param>
        /// <param name="challengeFilePath"></param>
        void GenerateHttpChallenge(out string challengeUrl, out string challengeContent, out string challengeFilePath);

        /// <summary>
        /// Register Order an Validate File HTTP Response
        /// </summary>
        /// <returns></returns>
        bool ValidateChallenge();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="certificateFriendlyName"></param>
        /// <param name="hostName"></param>
        /// <param name="certificatePath"></param>
        /// <returns></returns>
        CertificatePaths DownloadCertificate(
            string certificateFriendlyName,
            string hostName,
            string certificatePath,
            string password);
    }
}
