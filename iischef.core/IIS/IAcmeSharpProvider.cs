using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using iischef.utils;

namespace iischef.core.IIS
{
    public interface IAcmeSharpProvider : IDisposable
    {
        /// <summary>
        /// Initialize registration settings
        /// </summary>
        /// <param name="signerPath"></param>
        /// <param name="registrationPath"></param>
        /// <param name="email"></param>
        void InitRegistration(
            string signerPath,
            string registrationPath,
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
        /// <param name="certificatename"></param>
        /// <param name="mainhost"></param>
        /// <param name="certificatePath"></param>
        /// <param name="alternatehosts"></param>
        /// <returns></returns>
        CertificatePaths DownloadCertificate(
            string certificatename,
            string mainhost,
            string certificatePath,
            List<string> alternatehosts = null);
    }
}
