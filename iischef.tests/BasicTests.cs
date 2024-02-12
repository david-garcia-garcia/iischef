using iischef.cmdlet;
using iischef.core;
using iischef.core.IIS;
using iischef.core.IIS.AcmeProviders;
using iischef.core.IIS.Exceptions;
using iischef.utils;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Xunit.Abstractions;

namespace iischeftests
{
    /// <summary>
    /// 
    /// </summary>
    public class BasicTests : IClassFixture<ChefTestFixture>
    {
        /// <summary>
        /// The output helper
        /// </summary>
        protected ITestOutputHelper TestOutputHelper;

        /// <summary>
        /// Get an instance of BasicTests
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="output"></param>
        public BasicTests(ChefTestFixture fixture, ITestOutputHelper output)
        {
            this.TestOutputHelper = output;
        }

        protected void AssertStringDoesNotContains(string needle, string haystack)
        {
            Assert.False(
                haystack?.Contains(needle),
                $"Expected to find '{needle}' in '{haystack}'");
        }

        protected void AssertStringContains(string needle, string haystack)
        {
            Assert.True(
                haystack?.Contains(needle),
                $"Expected to find '{needle}' in '{haystack}'");
        }

        protected void AssertStringEquals(string str1, string str2)
        {
            Assert.True(
                str1 == str2,
                $"Expected to '{str1}' to equal '{str2}'");
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestSetupCentralCertificateStore()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestSetupCentralCertificateStore));
            var certificateNewLocation = Path.Combine("C:\\", $"ccs_{Guid.NewGuid()}");

            // Setup with defaults
            new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { CertStoreLocation = certificateNewLocation, PrivateKeyPassword = "mypassword", RegenerateStoreAccount = true }, logger);
            Assert.Equal(certificateNewLocation, UtilsIis.GetCentralCertificateStorePath(logger));

            // Cannot set user with invalid credentials
            Assert.ThrowsAny<BusinessRuleException>(() =>
            {
                new CmdSetupCentralCertificateStore().Run(
                    new CmdSetupCentralCertificateStoreArgs()
                    {
                        CertStoreLocation = certificateNewLocation,
                        PrivateKeyPassword = "mypassword",
                        UserName = "Administrator",
                        Password = "ASDGASDG"
                    }, logger);
            });

            Assert.Equal(CmdSetupCentralCertificateStore.CcsDefaultUserName, UtilsIis.GetCentralCertificateStoreUserNameRaw(logger));

            // Mess up with user
            using (var cmd = new ConsoleCommand())
            {
                cmd.RunCommand($"net user {CmdSetupCentralCertificateStore.CcsDefaultUserName} crap!passwo@3rd");
            }

            // If we redeploy, we will have issues because it won't be able to validate currently known credentials
            Assert.ThrowsAny<BusinessRuleException>(() =>
            {
                new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { }, logger);
            });

            // This one does work
            new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { Password = "crap!passwo@3rd" }, logger);

            // Regenerate store account
            new CmdSetupCentralCertificateStore().Run(
                new CmdSetupCentralCertificateStoreArgs()
                {
                    RegenerateStoreAccount = true
                }, logger);

            Directory.Delete(certificateNewLocation, true);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestSetupAcmeChallengeSiteAndGetSelfSignedCertificate()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestSetupAcmeChallengeSiteAndGetSelfSignedCertificate));

            var certificateNewLocation = Path.Combine("C:\\", $"ccs_{Guid.NewGuid()}");
            new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { CertStoreLocation = certificateNewLocation, PrivateKeyPassword = "mypassword" }, logger);

            var acmeChallengeSiteRoot = new AcmeChallengeSiteSetup(logger);
            acmeChallengeSiteRoot.SetupAcmeChallengeSite();

            new CmdRenewSsl().Run(
                new CmdRenewSslArgs()
                {
                    CertProvider = CertProvider.SelfSigned,
                    Force = false,
                    RegistrationMail = "foo@foo.com",
                    IssuerName = "foo",
                    RenewThresholdDays = 10,
                    HostName = "www.google.com"
                },
                logger);

            new CmdRenewSsl().Run(
                new CmdRenewSslArgs()
                {
                    CertProvider = CertProvider.SelfSigned,
                    Force = false,
                    RegistrationMail = "foo@foo.com",
                    IssuerName = "foo",
                    RenewThresholdDays = 10,
                    HostName = "www.google2.com"
                },
                logger);

            Assert.True((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google.com.pfx").Any()));
            Assert.True((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google2.com.pfx").Any()));

            // Fake a wildcard construct
            File.Copy(
                Path.Combine(UtilsIis.GetCentralCertificateStorePath(logger), "www.google.com.pfx"),
                Path.Combine(UtilsIis.GetCentralCertificateStorePath(logger), "_.google.com.pfx"));

            // If there is already a cert, and we try to deploy ccs, event if the pwd does not match we can redeploy
            new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { CertStoreLocation = certificateNewLocation, PrivateKeyPassword = "mypassword2" }, logger);

            string siteName = Guid.NewGuid().ToString();

            // Create a new site, and sync bindings to the CCS
            UtilsIis.ServerManagerRetry((sm) =>
            {
                sm.Sites.Add(siteName, "http", $"*:80:{siteName}", Path.GetTempPath());
                sm.CommitChanges();
            });

            SslBindingSync.SyncCcsBindingsToSite(siteName, logger);

            // Create a new site, and sync bindings to the CCS
            UtilsIis.ServerManagerRetry((sm) =>
            {
                var site = sm.Sites.Single(i => i.Name == siteName);

                try
                {
                    Assert.True(site.Bindings.Any((i) => i.BindingInformation == "*:443:www.google.com"));
                    Assert.True(site.Bindings.Any((i) => i.BindingInformation == "*:443:www.google2.com"));
                    Assert.True(site.Bindings.Any((i) => i.BindingInformation == "*:443:*.google.com"));
                }
                finally
                {
                    sm.Sites.Remove(site);
                    sm.CommitChanges();
                }
            });

            new CmdSetupCentralCertificateStore().Run(new CmdSetupCentralCertificateStoreArgs() { CertStoreLocation = certificateNewLocation, PrivateKeyPassword = "mypassword2" }, logger);
        }

        /// <summary>
        /// En caso de haber un fallo en el intento de renovación remota, se bloquean
        /// los intentos de renovación
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestFailedRenewalBlocksRenewalAttempts()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestSetupAcmeChallengeSiteAndGetSelfSignedCertificate));

            var certificateNewLocation = Path.Combine("C:\\", $"ccs_{Guid.NewGuid()}");

            new IISChefSetupCcs() { CertStoreLocation = certificateNewLocation, PrivateKeyPassword = "mypassword" }
                .Invoke()
                .OfType<object>()
                .ToList();

            var acmeChallengeSiteRoot = new AcmeChallengeSiteSetup(logger);
            acmeChallengeSiteRoot.SetupAcmeChallengeSite();

            Environment.SetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION", "true");

            Assert.ThrowsAny<Exception>(() =>
            {
                new CmdRenewSsl().Run(
                    new CmdRenewSslArgs()
                    {
                        CertProvider = CertProvider.SelfSigned,
                        Force = false,
                        RegistrationMail = "foo@foo.com",
                        IssuerName = "foo",
                        RenewThresholdDays = 10,
                        HostName = "www.google.com"
                    },
                    logger);
            });

            // No certificate was generated, because self signed fallback is not enabled
            Assert.False((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google.com.pfx").Any()));

            // Now, even if we remove the failure flag, renovation still "FAILS" because it is being blocked
            Environment.SetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION", null);

            Assert.ThrowsAny<RenewalCannotBeDoneException>(() =>
            {
                new CmdRenewSsl().Run(
                    new CmdRenewSslArgs()
                    {
                        CertProvider = CertProvider.SelfSigned,
                        Force = false,
                        RegistrationMail = "foo@foo.com",
                        IssuerName = "foo",
                        RenewThresholdDays = 10,
                        HostName = "www.google.com"
                    },
                    logger);
            });

            // No certificate was generated, because self signed fallback is not enabled
            Assert.False((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google.com.pfx").Any()));

            // For othe rdomains it works
            new CmdRenewSsl().Run(
                new CmdRenewSslArgs()
                {
                    CertProvider = CertProvider.SelfSigned,
                    Force = false,
                    RegistrationMail = "foo@foo.com",
                    IssuerName = "foo",
                    RenewThresholdDays = 10,
                    HostName = "www.google2.com",
                    DisableDnsValidation = true
                },
                logger);

            Assert.True((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google2.com.pfx").Any()));

            // But again not for the one with failure
            Assert.ThrowsAny<RenewalCannotBeDoneException>(() =>
            {
                new CmdRenewSsl().Run(
                    new CmdRenewSslArgs()
                    {
                        CertProvider = CertProvider.SelfSigned,
                        Force = false,
                        RegistrationMail = "foo@foo.com",
                        IssuerName = "foo",
                        RenewThresholdDays = 10,
                        HostName = "www.google.com"
                    },
                    logger);
            });

            // No certificate was generated, because self signed fallback is not enabled
            Assert.False((new DirectoryInfo(UtilsIis.GetCentralCertificateStorePath(logger)).EnumerateFiles("www.google.com.pfx").Any()));
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestAcmeSharpProviderMock()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestAcmeSharpProviderMock));
            var mock = new AcmeSharpProviderMock(logger, "www.mysampledomain.com");
            mock.DownloadCertificate("certificatexxx", "www.mydomain.com", null, "testpassword");
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestProvisionImportAndRemoveSelfSignedCertificate()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestProvisionImportAndRemoveSelfSignedCertificate));

            string friendlyName = "test-certificate-chef-TestProvisionImportAndRemoveSelfSignedCertificate";

            UtilsCertificate.RemoveCertificateFromLocalStoreByFriendlyName(friendlyName, out _);

            var tmpCertPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pfx");

            string authorityPfxPath = null; // Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pfx");

            UtilsCertificate.CreateSelfSignedCertificateAsPfx(
                "chef.testing.framework.domain.com",
                tmpCertPath,
                string.Empty,
                authorityPfxPath,
                logger,
                15);

            var certNoKey = new X509Certificate2(tmpCertPath, string.Empty, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
            Assert.Null(UtilsCertificate.TryFindPersistedPrivateKeyFilePath(certNoKey));

            string privateKeyPath;

            // We need to persist the key set in order for this to be available in IIS
            using (var cert = new X509Certificate2(
                tmpCertPath,
                string.Empty,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet))
            {
                privateKeyPath = UtilsCertificate.TryFindPersistedPrivateKeyFilePath(cert);
                Assert.NotNull(privateKeyPath);
                Assert.True(File.Exists(privateKeyPath));

                // Once instantiated with PersistKey, the key is persisted in the local machine
                using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    // Set friendly name before adding to store
                    cert.FriendlyName = friendlyName;
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(cert); // where cert is an X509Certificate object
                }
            }

            // Now remove..
            UtilsCertificate.RemoveCertificateFromLocalStoreByFriendlyName(friendlyName, out var removed);
            Assert.True(removed);

            Assert.False(File.Exists(privateKeyPath));

            File.Delete(tmpCertPath);

            if (!string.IsNullOrWhiteSpace(authorityPfxPath))
            {
                File.Delete(authorityPfxPath);
            }
        }
    }
}