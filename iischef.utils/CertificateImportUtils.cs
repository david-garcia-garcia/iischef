using iischef.logger;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace iischef.utils
{
    public static class CertificateImportUtils
    {
        /// <summary>
        /// Newly created accounts dont have their user profile initialized,
        /// and this means the store is
        /// </summary>
        public static void ImportCertificateToStore(
            string userName,
            string domainName,
            string password,
            string path,
            ILoggerInterface logger)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                domainName = ".";
            }

            using (var console = new ConsoleCommand(domainName, userName, password, "runas", false))
            {
                int exitCode = console.RunCommandAndWait("certutil -addstore -user \"My\" \"" + path + "\"", out string error);

                if (exitCode != 0)
                {
                    throw new Exception($"Error importing certificates: {error}");
                }
            }
        }

        /// <summary>
        /// Alternate version to import the certificate
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="path"></param>
        /// <param name="logger"></param>
        /// <exception cref="Exception"></exception>
        public static void ImportCertificateToStore2(
            string userName,
            string password,
            string path,
            ILoggerInterface logger)
        {
            X509Certificate2 certificate = new X509Certificate2(path);

            IntPtr accessToken;

            const int LOGON32_PROVIDER_DEFAULT = 0;
            const int LOGON32_LOGON_INTERACTIVE = 2;

            if (LogonUser(userName, Environment.MachineName, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out accessToken))
            {
                using (WindowsImpersonationContext impersonatedUser = WindowsIdentity.Impersonate(accessToken))
                {
                    using (X509Store userStore = new X509Store("My", StoreLocation.CurrentUser))
                    {
                        userStore.Open(OpenFlags.ReadWrite);

                        if (!userStore.Certificates.Contains(certificate))
                        {
                            userStore.Add(certificate);
                            logger.LogInfo(false, "Certificate imported: " + certificate.Subject);
                        }

                        userStore.Close();
                    }

                    impersonatedUser.Undo();
                }

                CloseHandle(accessToken);
            }
            else
            {
                throw new Exception("Error impersonating the user.");
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
