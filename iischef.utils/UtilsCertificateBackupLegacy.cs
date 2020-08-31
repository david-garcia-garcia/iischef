using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CERTENROLLLib;
using iischef.logger;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

namespace iischef.utils
{
    internal class UtilsCertificateBackupLegacy
    {
        /// <summary>
        /// Esta versión se usaba originalmente, pero utiliza API muy viejas y no permite tener un CA
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="friendlyName"></param>
        /// <param name="logger"></param>
        /// <param name="expirationDays"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static X509Certificate2 CreateSelfSignedCertificateOldImplementationNetFramework(
            string subjectName,
            string friendlyName,
            ILoggerInterface logger,
            int expirationDays = 90)
        {
            // create DN for subject
            var dn = new CX500DistinguishedName();
            dn.Encode("CN=" + subjectName, X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // create DN for the issuer
            var issuer = new CX500DistinguishedName();
            issuer.Encode("CN=ChefCertificate", X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // create a new private key for the certificate
            CX509PrivateKey privateKey = new CX509PrivateKey();
            privateKey.ProviderName = "Microsoft Base Cryptographic Provider v1.0";
            privateKey.MachineContext = true;
            privateKey.Length = 2048;
            privateKey.KeySpec = X509KeySpec.XCN_AT_SIGNATURE; // use is not limited
            privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;
            privateKey.Create();

            // Use the stronger SHA512 hashing algorithm
            var hashobj = new CObjectId();
            hashobj.InitializeFromAlgorithmName(
                ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
                ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
                AlgorithmFlags.AlgorithmFlagsNone, 
                "SHA512");

            // add extended key usage if you want - look at MSDN for a list of possible OIDs
            var oid = new CObjectId();
            oid.InitializeFromValue("1.3.6.1.5.5.7.3.1"); // SSL server
            var oidlist = new CObjectIds();
            oidlist.Add(oid);
            var eku = new CX509ExtensionEnhancedKeyUsage();
            eku.InitializeEncode(oidlist);

            // Create the self signing request
            var cert = new CX509CertificateRequestCertificate();
            cert.InitializeFromPrivateKey(X509CertificateEnrollmentContext.ContextMachine, privateKey, string.Empty);
            cert.Subject = dn;
            cert.Issuer = issuer;
            cert.NotBefore = DateTime.Now;

            // this cert expires immediately. Change to whatever makes sense for you
            cert.NotAfter = DateTime.Now.AddDays(expirationDays);
            cert.X509Extensions.Add((CX509Extension)eku); // add the EKU
            cert.HashAlgorithm = hashobj; // Specify the hashing algorithm
            cert.Encode(); // encode the certificate

            // Do the final enrollment process
            var enroll = new CX509Enrollment();
            enroll.InitializeFromRequest(cert); // load the certificate
            enroll.CertificateFriendlyName = friendlyName; // Optional: add a friendly name
            string csr = enroll.CreateRequest(); // Output the request in base64

            // and install it back as the response
            enroll.InstallResponse(
                InstallResponseRestrictionFlags.AllowUntrustedCertificate,
                csr,
                EncodingType.XCN_CRYPT_STRING_BASE64, 
                string.Empty); // no password

            // output a base64 encoded PKCS#12 so we can import it back to the .Net security classes
            var base64Encoded = enroll.CreatePFX(
                string.Empty, // no password, this is for internal consumption
                PFXExportOptions.PFXExportChainWithRoot);

            // Delete the key
            var storePath = UtilsCertificate.FindKeyStoragePath(privateKey.UniqueContainerName);

            if (string.IsNullOrWhiteSpace(storePath))
            {
                logger.LogWarning(false, $"Unable to determine private key store path for key with UCN '{privateKey.UniqueContainerName}', keys will build up in disk storage.");
            }
            else
            {
                File.Delete(storePath);
            }

            // instantiate the target class with the PKCS#12 data (and the empty password)
            var crt = new X509Certificate2(
                Convert.FromBase64String(base64Encoded),
                string.Empty,
                
                // mark the private key as exportable (this is usually what you want to do)
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

            // Remove the locally signed certificate from the local store, as we are only interested in
            // the exported PFX file
            UtilsCertificate.RemoveCertificateFromLocalStoreByThumbprint(crt.Thumbprint);

            return crt;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/22230745/generate-a-self-signed-certificate-on-the-fly
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="issuerName"></param>
        /// <param name="issuerPrivKey"></param>
        /// <returns></returns>
        public static X509Certificate2 GenerateSelfSignedCertificate(
            string subjectName,
            string issuerName,
            AsymmetricKeyParameter issuerPrivKey)
        {
            const int keyStrength = 2048;

            // Generating Random Numbers
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);

            // The Certificate Generator
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

            // Serial Number
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);

            // Signature Algorithm
            const string signatureAlgorithm = "SHA256WithRSA";
            certificateGenerator.SetSignatureAlgorithm(signatureAlgorithm);

            // Issuer and Subject Name
            X509Name subjectDn = new X509Name(subjectName);
            X509Name issuerDn = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDn);
            certificateGenerator.SetSubjectDN(subjectDn);

            // Valid For
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(2);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // Subject Public Key
            var keyGenerationParameters = new KeyGenerationParameters(random, keyStrength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Selfsign certificate
            Org.BouncyCastle.X509.X509Certificate certificate = certificateGenerator.Generate(issuerPrivKey, random);

            // Corresponding private key
            PrivateKeyInfo info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);

            // Merge into X509Certificate2
            X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded(), string.Empty, X509KeyStorageFlags.EphemeralKeySet);

            Asn1Sequence seq = (Asn1Sequence)Asn1Object.FromByteArray(info.ParsePrivateKey().GetDerEncoded());
            if (seq.Count != 9)
            {
                throw new PemException("malformed sequence in RSA private key");
            }

            RsaPrivateKeyStructure rsa = new RsaPrivateKeyStructure(seq);
            RsaPrivateCrtKeyParameters rsaparams = new RsaPrivateCrtKeyParameters(
                rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent, rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);

            x509.PrivateKey = ToDotNetKey(rsaparams); // x509.PrivateKey = DotNetUtilities.ToRSA(rsaparams);
            return x509;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/22230745/generate-a-self-signed-certificate-on-the-fly
        /// </summary>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static AsymmetricAlgorithm ToDotNetKey(RsaPrivateCrtKeyParameters privateKey)
        {
            var cspParams = new CspParameters
            {
                KeyContainerName = Guid.NewGuid().ToString(),
                KeyNumber = (int)KeyNumber.Exchange,
                Flags = CspProviderFlags.UseMachineKeyStore
            };

            var rsaProvider = new RSACryptoServiceProvider(cspParams);
            var parameters = new RSAParameters
            {
                Modulus = privateKey.Modulus.ToByteArrayUnsigned(),
                P = privateKey.P.ToByteArrayUnsigned(),
                Q = privateKey.Q.ToByteArrayUnsigned(),
                DP = privateKey.DP.ToByteArrayUnsigned(),
                DQ = privateKey.DQ.ToByteArrayUnsigned(),
                InverseQ = privateKey.QInv.ToByteArrayUnsigned(),
                D = privateKey.Exponent.ToByteArrayUnsigned(),
                Exponent = privateKey.PublicExponent.ToByteArrayUnsigned()
            };

            rsaProvider.ImportParameters(parameters);
            return rsaProvider;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/22230745/generate-a-self-signed-certificate-on-the-fly
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="subjectKeyPair"></param>
        /// <param name="resultKeyPair"></param>
        /// <returns></returns>
        public static X509Certificate2 GenerateCaCertificate(
            string subjectName,
            out AsymmetricCipherKeyPair resultKeyPair,
            AsymmetricCipherKeyPair subjectKeyPair = null)
        {
            const int keyStrength = 2048;

            // Generating Random Numbers
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);

            // The Certificate Generator
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

            // Serial Number
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);

            // Signature Algorithm
            const string signatureAlgorithm = "SHA256WithRSA";
            certificateGenerator.SetSignatureAlgorithm(signatureAlgorithm);

            // Issuer and Subject Name
            X509Name subjectDn = new X509Name(subjectName);
            X509Name issuerDn = subjectDn;
            certificateGenerator.SetIssuerDN(issuerDn);
            certificateGenerator.SetSubjectDN(subjectDn);

            // Valid For
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(100);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // Generate a key pair if none was provided
            if (subjectKeyPair == null)
            {
                KeyGenerationParameters keyGenerationParameters = new KeyGenerationParameters(random, keyStrength);
                RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
                keyPairGenerator.Init(keyGenerationParameters);
                subjectKeyPair = keyPairGenerator.GenerateKeyPair();
            }

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Generating the Certificate
            AsymmetricCipherKeyPair issuerKeyPair = subjectKeyPair;

            // Selfsign certificate
            Org.BouncyCastle.X509.X509Certificate certificate = certificateGenerator.Generate(issuerKeyPair.Private, random);
            X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded(), string.Empty, X509KeyStorageFlags.EphemeralKeySet);

            resultKeyPair = issuerKeyPair;

            return x509;
        }
    }
}
