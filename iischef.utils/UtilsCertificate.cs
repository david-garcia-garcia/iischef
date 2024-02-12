using iischef.logger;
using Security.Cryptography.X509Certificates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using X509Store = System.Security.Cryptography.X509Certificates.X509Store;

namespace iischef.utils
{
    /// <summary>
    /// 
    /// </summary>
    public static class UtilsCertificate
    {
        /// <summary>
        /// Generate a PFX file
        /// </summary>
        /// <param name="crtFile"></param>
        /// <param name="keyFile"></param>
        /// <param name="pfxFile"></param>
        /// <param name="certificateFilePassword"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreatePfXfromPem(
            string crtFile,
            string keyFile,
            string pfxFile,
            string certificateFilePassword)
        {
            string dat;
            using (StreamReader stream = new StreamReader(crtFile))
            {
                dat = stream.ReadToEnd();
            }

            OpenSSL.Core.BIO certbio = new OpenSSL.Core.BIO(dat);
            OpenSSL.X509.X509Certificate x = new OpenSSL.X509.X509Certificate(certbio);

            string datakey;

            using (StreamReader stream = new StreamReader(keyFile))
            {
                datakey = stream.ReadToEnd();
            }

            OpenSSL.Crypto.CryptoKey key = OpenSSL.Crypto.CryptoKey.FromPrivateKey(datakey, certificateFilePassword);
            OpenSSL.Core.BIO a = OpenSSL.Core.BIO.File(pfxFile, "wb");
            OpenSSL.Core.Stack<OpenSSL.X509.X509Certificate> ca = new OpenSSL.Core.Stack<OpenSSL.X509.X509Certificate>();
            OpenSSL.X509.PKCS12 p12 = new OpenSSL.X509.PKCS12(certificateFilePassword, key, x, ca);

            p12.Write(a);

            key.Dispose();
            p12.Dispose();
            a.Dispose();
        }

        /// <summary>
        /// TODO: Asegurar que no saturamos las certificaciones locales...
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RemoveCertificateFromLocalStoreByThumbprint(string thumbPrint)
        {
            using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);

                // You could also use a more specific find type such as X509FindType.FindByThumbprint
                X509Certificate2Collection col =
                    store.Certificates.Find(X509FindType.FindByThumbprint, thumbPrint, false);

                foreach (var cert in col)
                {
                    // Remove the certificate
                    store.Remove(cert);
                    TryRemovePrivateKey(cert);
                }

                store.Close();
            }
        }

        /// <summary>
        /// Remove a certificate using it's friendly name
        /// </summary>
        /// <param name="friendlyName"></param>
        /// <param name="removed"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RemoveCertificateFromLocalStoreByFriendlyName(string friendlyName, out bool removed)
        {
            removed = false;

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);

                // You could also use a more specific find type such as X509FindType.FindByThumbprint
                X509Certificate2Collection col =
                    store.Certificates;

                foreach (var cert in col)
                {
                    if (cert.FriendlyName == friendlyName)
                    {
                        store.Remove(cert);
                        TryRemovePrivateKey(cert);
                        removed = true;
                        break;
                    }
                }

                store.Close();
            }
        }

        /// <summary>
        /// Try to remove the local private key for the certificate
        /// </summary>
        /// <param name="certificate"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryRemovePrivateKey(X509Certificate2 certificate)
        {
            var uniqueKeyContainerName = TryFindUniqueContainerName(certificate);

            if (string.IsNullOrWhiteSpace(uniqueKeyContainerName))
            {
                return;
            }

            // Delete the key
            var storePath = FindKeyStoragePath(uniqueKeyContainerName);

            if (!string.IsNullOrWhiteSpace(storePath))
            {
                File.Delete(storePath);
            }
        }

        /// <summary>
        /// Try to the storage path for the persisted private key of a certificate
        /// </summary>
        /// <param name="certificate"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TryFindPersistedPrivateKeyFilePath(X509Certificate2 certificate)
        {
            var uniqueKeyContainerName = TryFindUniqueContainerName(certificate);

            if (string.IsNullOrWhiteSpace(uniqueKeyContainerName))
            {
                return null;
            }

            // Delete the key
            return FindKeyStoragePath(uniqueKeyContainerName);
        }

        /// <summary>
        /// Try to find the unique container name of a certificate
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TryFindUniqueContainerName(X509Certificate2 cert)
        {
            if (cert.HasCngKey())
            {
                return cert.GetCngPrivateKey().UniqueName;
            }
            else if (cert.HasPrivateKey)
            {
                if (cert.PrivateKey is RSACryptoServiceProvider privateKey)
                {
                    var uniqueKeyContainerName = privateKey.CspKeyContainerInfo.UniqueKeyContainerName;
                    return uniqueKeyContainerName;
                }
            }

            return null;
        }

        /// <summary>
        /// Find certificate in store
        /// </summary>
        /// <param name="thumbPrint"></param>
        /// <param name="storeName"></param>
        /// <param name="storeLocation"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static X509Certificate2 FindCertificate(string thumbPrint, StoreName storeName, StoreLocation storeLocation)
        {
            X509Certificate2 certificate = null;

            X509Store store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly | OpenFlags.IncludeArchived);

            // You could also use a more specific find type such as X509FindType.FindByThumbprint
            X509Certificate2Collection col =
                store.Certificates.Find(X509FindType.FindByThumbprint, thumbPrint, false);

            foreach (var cert in col)
            {
                certificate = cert;
            }

            store.Close();

            return certificate;
        }

        /// <summary>
        /// Crear un certificado válido para ser usado como root/ca. Ni lo importa, ni crea claves privadas
        /// en la máquina (ephemeral)
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="country"></param>
        /// <param name="expirationDays"></param>
        /// <param name="organization"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static X509Certificate2 CreateSelfSignedCertificateNewImplementationCa(
            string subjectName,
            string organization,
            string country,
            int expirationDays = 90)
        {
            var ecdsa = ECDsa.Create(); // generate asymmetric key pair

            CertificateRequest request = new CertificateRequest(
                $"CN={subjectName}, O={organization}, C={country}",
                ecdsa,
                HashAlgorithmName.SHA256);

            // Indicate this certificate is to be used as a TLS Server via the EKU extension 
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            // Tell that this is a root certificate
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 5, false));

            // Indicate the Subject Alternative Names requested 

            // SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            // sanBuilder.AddDnsName("www.adatum.com");
            // sanBuilder.AddDnsName("adatum.com");
            // request.CertificateExtensions.Add(sanBuilder.Build());

            DateTimeOffset notBefore = DateTimeOffset.UtcNow;
            DateTimeOffset notAfter = notBefore.AddDays(expirationDays);

            X509Certificate2 certWithPrivateKey = request.CreateSelfSigned(notBefore, notAfter);
            return certWithPrivateKey;
        }

        /// <summary>
        /// Create a self signed certificate, optionally providing a locally existente CA certificate
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="organization"></param>
        /// <param name="country"></param>
        /// <param name="authority">Optional authority cert to sign this one against.</param>
        /// <param name="expirationDays"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static X509Certificate2 GenerateSelfSignedCertificate(
            string subjectName,
            string organization,
            string country,
            X509Certificate2 authority = null,
            int expirationDays = 90)
        {
            // Have this certificate work with localhost, etc..
            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={subjectName}, O={organization}, C={country}");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                request.CertificateExtensions.Add(sanBuilder.Build());

                DateTimeOffset notBefore = DateTimeOffset.UtcNow;
                DateTimeOffset notAfter = notBefore.AddDays(expirationDays);

                X509Certificate2 certWithPrivateKey;

                if (authority == null)
                {
                    certWithPrivateKey = request.CreateSelfSigned(notBefore, notAfter);
                }
                else
                {
                    byte[] serialnumber = Encoding.ASCII.GetBytes(DateTime.UtcNow.ToString("MMMMddyyyyHHmmss"));
                    certWithPrivateKey = request.Create(authority, notBefore, notAfter, serialnumber);
                }

                return certWithPrivateKey;
            }
        }

        /// <summary>
        /// Genera el certificaod y lo guarda como PFX
        /// </summary>
        /// <param name="subjectName"></param>
        /// <param name="pfxFilePath"></param>
        /// <param name="certificatePassword"></param>
        /// <param name="caPfxPath"></param>
        /// <param name="logger"></param>
        /// <param name="expirationDays"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateSelfSignedCertificateAsPfx(
            string subjectName,
            string pfxFilePath,
            string certificatePassword,
            string caPfxPath,
            ILoggerInterface logger,
            int expirationDays = 90)
        {
            if (!string.IsNullOrWhiteSpace(caPfxPath))
            {
                // Signing using a CA is not yet implemented, mostly because de CA distribution strategy
                // need to be set, as the generated certificate is not stand-alone and requires de CA certificate
                // for the private key.
                throw new NotImplementedException("Not implemented. Read comments.");
            }

            // If there is no authority PFX create one
            if (!string.IsNullOrWhiteSpace(caPfxPath) && !File.Exists(caPfxPath))
            {
                using (X509Certificate2 rootCertificate = CreateSelfSignedCertificateNewImplementationCa("Chef Deployer", "Chef Deployer", "ES", 36500))
                {
                    File.WriteAllBytes(caPfxPath, rootCertificate.Export(X509ContentType.Pfx, certificatePassword));
                    rootCertificate.Reset();
                }
            }

            X509Certificate2 authority = null;

            // Read the authority, so we can sign using their private key
            if (!string.IsNullOrWhiteSpace(caPfxPath) && File.Exists(caPfxPath))
            {
                X509Certificate2Collection collection = new X509Certificate2Collection();
                collection.Import(caPfxPath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
                authority = collection[0];
            }

            // Save in the local store
            using (var cert = GenerateSelfSignedCertificate(subjectName, subjectName, "ES", authority, expirationDays))
            {
                File.WriteAllBytes(pfxFilePath, cert.Export(X509ContentType.Pfx, certificatePassword));
                cert.Reset();
                logger.LogInfo(false, "Generating self signed certificate. [subject={0}]", subjectName);
            }
        }

        /// <summary>
        /// Not very reliable... but does the job most of the time
        /// </summary>
        /// <param name="privateKeyUniqueContainerId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string FindKeyStoragePath(string privateKeyUniqueContainerId)
        {
            // https://snede.net/the-most-dangerous-constructor-in-net/

            List<string> potentialKeyLocations = new List<string>();
            potentialKeyLocations.Add("%APPDATA%\\Microsoft\\Crypto\\Keys");
            potentialKeyLocations.Add("%ALLUSERSPROFILE%\\Microsoft\\Crypto\\SystemKeys");
            potentialKeyLocations.Add("%WINDIR%\\ServiceProfiles\\LocalService");
            potentialKeyLocations.Add("%WINDIR%\\ServiceProfiles\\NetworkService");
            potentialKeyLocations.Add("%ALLUSERSPROFILE%\\Microsoft\\Crypto\\Keys");
            potentialKeyLocations.Add("%ALLUSERSPROFILE%\\Microsoft\\Crypto\\RSA\\MachineKeys");
            potentialKeyLocations.Add("%ALLUSERSPROFILE%\\Microsoft\\Crypto\\DSS\\MachineKeys");

            foreach (var key in potentialKeyLocations)
            {
                string path = Environment.ExpandEnvironmentVariables(Path.Combine(key, privateKeyUniqueContainerId));

                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ChangePrivateKeyPwd(string pfxPath, string pfxPassword, string pfxNewPassword)
        {
            // Load the existing certificate with the provided password
            using (var cert = new X509Certificate2(pfxPath, pfxPassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet))
            {
                // Export the private key and create a new encrypted PFX file with the new password
                var newPfxBytes = cert.Export(X509ContentType.Pfx, pfxNewPassword);
                File.WriteAllBytes(pfxPath, newPfxBytes);
                return true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="certificateFile"></param>
        /// <param name="certificatePassword"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CheckPfxPassword(string certificateFile, string certificatePassword)
        {
            if (!File.Exists(certificateFile))
            {
                throw new Exception($"Certificate file not found {certificateFile}");
            }

            try
            {
                using (var certificate = new X509Certificate2(certificateFile, certificatePassword))
                {
                }
            }
            catch (CryptographicException)
            {
                return false;
            }

            return true;
        }
    }
}
