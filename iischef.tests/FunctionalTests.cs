using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using iischef.logger;
using iischef.utils;
using iischef.utils.WindowsAccount;
using Microsoft.Web.Administration;
using Xunit;

namespace iischeftests
{
    public class FunctionalTests : IClassFixture<ChefTestFixture>
    {
        protected ChefTestFixture Fixture;

        public FunctionalTests(ChefTestFixture fixture)
        {
            this.Fixture = fixture;
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestIisGetConfigIsolationPath()
        {
            Assert.NotNull(UtilsIis.GetConfigIsolationPath());
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestHostnameMatching()
        {
            Assert.True(UtilsIis.HostNameMatch("www.google.com", "*.google.com", "*"));
            Assert.False(UtilsIis.HostNameMatch("www.google.es", "*.google.com", "*"));
            Assert.False(UtilsIis.HostNameMatch("www.windows.com", "*.google.com", "*"));
            Assert.True(UtilsIis.HostNameMatch("*.google.com", "_.google.com", "_"));
            Assert.True(UtilsIis.HostNameMatch("subdomain.google.com", "_.google.com", "_"));
            Assert.True(UtilsIis.HostNameMatch("www.google.com", "www.google.com", "*"));

            // Hostname are case insensitive
            Assert.True(UtilsIis.HostNameMatch("www.GooGle.com", "Www.google.com", "*"));
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestCanGetErrorCode()
        {
            var code = ErrorUtils.GetExceptionErrorCode(new PrincipalOperationException("xx", 365));
            Assert.Equal(365, code);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestFileCopyPreserveTimestamps()
        {
            var file = Path.GetTempFileName();
            var fileInfo = new FileInfo(file);
            fileInfo.LastWriteTimeUtc = DateTime.Now.AddDays(-5);

            var copiedFile = Path.GetTempFileName();
            File.Delete(copiedFile);
            fileInfo.CopyTo(copiedFile);
            var fileInfoCopied = new FileInfo(copiedFile);

            // Default file system API does not preserve timestamps
            Assert.Equal(fileInfoCopied.LastWriteTimeUtc, fileInfo.LastWriteTimeUtc);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void ReadBoundCertificates()
        {
            var certificates = UtilsIis.GetBoundCertificates();
            Assert.NotEmpty(certificates);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void DeleteHttpsBindings()
        {
            var logger = new TestLogsLogger(this, nameof(this.DeleteHttpsBindings));

            using (ServerManager sm = new ServerManager())
            {
                var site = sm.Sites.Add("site0", Path.GetTempPath(), 443);
                site.Bindings.Add("127.0.0.1:5879:site0", null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                site.Bindings.Add("127.0.0.1:5879:site1", null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                sm.CommitChanges();
            }

            Thread.Sleep(500);
            var certificates = UtilsIis.GetBoundCertificates();
            Assert.True(certificates.Any((i) =>
                i.Attributes.Any((j) => j.Key == "Central Certificate Store" && j.Value == "5879")));

            using (ServerManager sm = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(sm, "site0", logger).Single();
                UtilsIis.RemoveSiteBindings(site, sm, (i) => i.Host == "site1", logger);
                sm.CommitChanges();
            }

            certificates = UtilsIis.GetBoundCertificates();
            Assert.True(certificates.Any((i) =>
                i.Attributes.Any((j) => j.Key == "Central Certificate Store" && j.Value == "5879")));

            using (ServerManager sm = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(sm, "site0", logger).Single();
                UtilsIis.RemoveSiteBindings(site, sm, (i) => i.Host == "site0", logger);
                sm.CommitChanges();
            }

            Thread.Sleep(500);
            certificates = UtilsIis.GetBoundCertificates();
            Assert.False(certificates.Any((i) =>
                i.Attributes.Any((j) => j.Key == "Central Certificate Store" && j.Value == "5879")));

            using (ServerManager sm = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(sm, "site0", logger).Single();
                site.Bindings.Add("127.0.0.1:5879:site0", null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                site.Bindings.Add("127.0.0.1:5879:site1", null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                sm.CommitChanges();
            }

            Thread.Sleep(500);
            certificates = UtilsIis.GetBoundCertificates();
            Assert.True(certificates.Any((i) =>
                i.Attributes.Any((j) => j.Key == "Central Certificate Store" && j.Value == "5879")));

            using (ServerManager sm = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(sm, "site0", logger).Single();
                UtilsIis.RemoveSite(site, sm, logger);
                sm.CommitChanges();
            }

            Thread.Sleep(500);
            certificates = UtilsIis.GetBoundCertificates();
            Assert.False(certificates.Any((i) =>
                i.Attributes.Any((j) => j.Key == "Central Certificate Store" && j.Value == "5879")));
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestCannotDeleteDirectoriesWithSmallDepth()
        {
            var directory = Directory.CreateDirectory("c:\\testdirectory");

            var logger = new TestLogsLogger(this, nameof(this.TestCannotDeleteDirectoriesWithSmallDepth));

            Assert.Throws<InvalidOperationException>(() =>
            {
                UtilsSystem.DeleteDirectory(directory.FullName, logger);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                UtilsSystem.DeleteDirectory(directory.FullName, logger, 5);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                UtilsSystem.DeleteDirectory(directory.FullName, logger);
            });

            directory.Delete();
        }

        /// <summary>
        /// Converting PEM to PFX requires some 3d party libraries that brake every once in a while...
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TestPfxFromPem()
        {
            string pfxFilePath = Path.GetTempPath() + "ssl-pfx-test-" + Guid.NewGuid() + ".pfx";

            string certificatePassword = null;
            string crtFilePath = UtilsSystem.GetResourceFileAsPath("certificate_files_example\\a0e8efb7cca4452ed304b1d9614ec89d-crt.pem");
            string keyFilePath = UtilsSystem.GetResourceFileAsPath("certificate_files_example\\a0e8efb7cca4452ed304b1d9614ec89d-key.pem");

            UtilsCertificate.CreatePfXfromPem(crtFilePath, keyFilePath, pfxFilePath, certificatePassword);

            // Make sure that the certificate file is valid and works
            X509Certificate2Collection collection = new X509Certificate2Collection();

            collection.Import(pfxFilePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
            var originalCert = collection[0];
            Assert.Equal("033679B39C0CDA50C745ABD173FB0DD381A1", originalCert.SerialNumber);

            File.Delete(pfxFilePath);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestIpParser()
        {
            Assert.Equal("192.168.5.3", UtilsIis.GetSubnetAndMaskFromCidr("192.168.5.3/20").Item1.ToString());
            Assert.Equal("255.255.240.0", UtilsIis.GetSubnetAndMaskFromCidr("192.168.5.3/20").Item2.ToString());

            Assert.Equal("192.168.5.3", UtilsIis.GetSubnetAndMaskFromCidr("192.168.5.3").Item1.ToString());
            Assert.Null(UtilsIis.GetSubnetAndMaskFromCidr("192.168.5.3").Item2);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestCleanFastCgi()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestCleanFastCgi));
            UtilsIis.CleanUpFastCgiSettings(logger);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestCleanUpStaleApplicationPools()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestCleanUpStaleApplicationPools));
            UtilsIis.CleanUpStaleApplicationPools(logger);
        }

        /// <summary>
        /// Test certificate import to store strategy 1
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestImportCertificateToStoreV1()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestImportCertificateToStoreV1));

            string userName = "TestUser1";
            string pwd = "@Lpebc!!-password1";

            try
            {
                UtilsAccountManagement.UpsertUser(userName, pwd, logger);
                UtilsAccountManagement.EnsureUserInGroup(userName, UtilsWindowsAccounts.WELL_KNOWN_SID_ADMINISTRATORS, logger);

                string certificateUrl = "https://letsencrypt.org/certs/isrg-root-x1-cross-signed.pem";

                List<X509Certificate2> certificates = new List<X509Certificate2>();
                WebClient client = new WebClient();
                byte[] certBytes = client.DownloadData(certificateUrl);
                certificates.Add(new X509Certificate2(certBytes));

                Advapi32Extern.LogonUser(userName, Environment.MachineName, pwd, 2, 0, out var accessToken);

                List<X509Certificate2> missingCertificates = new List<X509Certificate2>();

                WindowsIdentity.RunImpersonated(accessToken, () =>
                {
                    using (X509Store userStore = new X509Store("My", StoreLocation.CurrentUser))
                    {
                        userStore.Open(OpenFlags.ReadOnly);

                        foreach (var c in certificates)
                        {
                            if (!userStore.Certificates.Contains(c))
                            {
                                missingCertificates.Add(c);
                            }
                        }

                        userStore.Close();
                    }
                });

                foreach (var missingCertificate in missingCertificates)
                {
                    logger.LogInfo(false, "Importing certificate to My store: " + missingCertificate.Subject);

                    var tempPath = Path.Combine(
                        Environment.GetEnvironmentVariable("PUBLIC"),
                        Guid.NewGuid().ToString() + "tmp");

                    try
                    {
                        File.WriteAllBytes(tempPath, missingCertificate.GetRawCertData());
                        CertificateImportUtils.ImportCertificateToStore(userName, null, pwd, tempPath, logger);
                    }
                    finally
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            finally
            {
                UtilsAccountManagement.DeleteUser(userName, logger);
            }
        }

        /// <summary>
        /// Test certificate import to store strategy 1
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestImportCertificateToStoreV2()
        {
            // This approach does not work

            ////var logger = new TestLogsLogger(this, nameof(this.TestImportCertificateToStoreV1));

            ////var identity = IdentityWrapper.FromUserInfo("TestUser1", "testuser1", "password1", "TestUser1", null);

            ////try
            ////{
            ////    UtilsWindowsAccounts.EnsureUserExists(identity, logger);

            ////    UtilsWindowsAccounts.EnsureUserInGroup(
            ////        identity,
            ////        IdentityWrapper.FromSid(UtilsWindowsAccounts.WELL_KNOWN_SID_ADMINISTRATORS, null, TypeHint.Group),
            ////        logger,
            ////        null);

            ////    string certificateUrl = "https://letsencrypt.org/certs/isrg-root-x1-cross-signed.pem";

            ////    List<X509Certificate2> certificates = new List<X509Certificate2>();
            ////    WebClient client = new WebClient();
            ////    byte[] certBytes = client.DownloadData(certificateUrl);
            ////    certificates.Add(new X509Certificate2(certBytes));

            ////    Advapi32Extern.LogonUser(identity.Name, Environment.MachineName, identity.Password, 2, 0, out var accessToken);

            ////    List<X509Certificate2> missingCertificates = new List<X509Certificate2>();

            ////    WindowsIdentity.RunImpersonated(accessToken, () =>
            ////    {
            ////        using (X509Store userStore = new X509Store("My", StoreLocation.CurrentUser))
            ////        {
            ////            userStore.Open(OpenFlags.ReadOnly);

            ////            foreach (var c in certificates)
            ////            {
            ////                if (!userStore.Certificates.Contains(c))
            ////                {
            ////                    missingCertificates.Add(c);
            ////                }
            ////            }

            ////            userStore.Close();
            ////        }
            ////    });

            ////    foreach (var missingCertificate in missingCertificates)
            ////    {
            ////        logger.LogInfo(false, "Importing certificate to My store: " + missingCertificate.Subject);

            ////        var tempPath = Path.Combine(
            ////            Environment.GetEnvironmentVariable("PUBLIC"),
            ////            Guid.NewGuid().ToString() + "tmp");

            ////        try
            ////        {
            ////            File.WriteAllBytes(tempPath, missingCertificate.GetRawCertData());
            ////            CertificateImportUtils.ImportCertificateToStore2(identity.Name, identity.Password, tempPath, logger);
            ////        }
            ////        finally
            ////        {
            ////            File.Delete(tempPath);
            ////        }
            ////    }
            ////}
            ////finally
            ////{
            ////    UtilsWindowsAccounts.DeleteUser(identity, null, true);
            ////}
        }

        /// <summary>
        /// Este test comprueba el funcionamiento del método que hace resolución
        /// inversa de symlinks
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestResolveJunctionPath()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestResolveJunctionPath));

            string testPath = UtilsSystem.GetTempPath("symlink_test" + Guid.NewGuid());

            // Probar resolución de nivel 1

            string test1OriginalPath = UtilsSystem.EnsureDirectoryExists(Path.Combine(testPath, "test1"), true);
            string test1LinkPath = Path.Combine(testPath, "test1_link");
            string test1JunctionPath = Path.Combine(testPath, "test1_junction");

            UtilsJunction.EnsureLink(test1LinkPath, test1OriginalPath, logger, true, linkType: UtilsJunction.LinkTypeRequest.Symlink);
            UtilsJunction.EnsureLink(test1JunctionPath, test1OriginalPath, logger, true, linkType: UtilsJunction.LinkTypeRequest.Junction);

            Assert.Equal(test1OriginalPath, UtilsJunction.ResolvePath(test1LinkPath));
            Assert.Equal(test1OriginalPath, UtilsJunction.ResolvePath(test1JunctionPath));

            // Probar resolución de subdirectorio existente y no existente

            string test2OriginalPath = UtilsSystem.EnsureDirectoryExists(Path.Combine(testPath, "test2"), true);
            string test2LinkPath = Path.Combine(testPath, "test2_link");
            string test2JunctionPath = Path.Combine(testPath, "test2_junction");

            UtilsJunction.EnsureLink(test2LinkPath, test2OriginalPath, logger, true, linkType: UtilsJunction.LinkTypeRequest.Symlink);
            UtilsJunction.EnsureLink(test2JunctionPath, test2OriginalPath, logger, true, linkType: UtilsJunction.LinkTypeRequest.Junction);

            string test2LinkSubDir = UtilsSystem.EnsureDirectoryExists(Path.Combine(test2LinkPath, "sub1", "sub2"), true);
            string test2JunctionSubDir = UtilsSystem.EnsureDirectoryExists(Path.Combine(test2JunctionPath, "sub3", "sub4"), true);

            Assert.Equal(Path.Combine(test2OriginalPath, "sub1", "sub2"), UtilsJunction.ResolvePath(test2LinkSubDir));
            Assert.Equal(Path.Combine(test2OriginalPath, "sub3", "sub4"), UtilsJunction.ResolvePath(test2JunctionSubDir));

            // Ahora subdirectorios que no existen
            Assert.Equal(Path.Combine(test2OriginalPath, "sub4", "sub5"), UtilsJunction.ResolvePath(Path.Combine(test2LinkPath, "sub4", "sub5")));
            Assert.Equal(Path.Combine(test2OriginalPath, "sub6", "sub7"), UtilsJunction.ResolvePath(Path.Combine(test2JunctionPath, "sub6", "sub7")));

            // Ahora una cadena de enlaces dentro de otro enlace...
            string test3LinkSubDir = Path.Combine(test2LinkPath, "sub8");

            UtilsSystem.EnsureDirectoryExists(Path.Combine(test2LinkPath, "test3"), true);
            UtilsJunction.EnsureLink(test3LinkSubDir, Path.Combine(test2LinkPath, "test3"), logger, true, linkType: UtilsJunction.LinkTypeRequest.Symlink);

            Assert.Equal(Path.Combine(test2OriginalPath, "test3"), UtilsJunction.ResolvePath(test3LinkSubDir));

            UtilsSystem.DeleteDirectory(testPath, logger, 2);

            // Non existent and malformed network uri get reconstructed as-is
            string testNetworkUri = "\\\\147.83.73.25\\a\\b\\c\\\\d";
            Assert.Equal(testNetworkUri, UtilsJunction.ResolvePath(testNetworkUri));
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestSymlinksAreNotRemoved()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestSymlinksAreNotRemoved));

            string testPath = UtilsSystem.GetTempPath("symlink_test" + Guid.NewGuid());

            var pathWebsite = Path.Combine(testPath, "website");
            var pathContentsPersistent = Path.Combine(testPath, "content_store_persistent");

            Directory.CreateDirectory(pathWebsite);
            Directory.CreateDirectory(pathContentsPersistent);
            Directory.CreateDirectory(Path.Combine(pathContentsPersistent, "empty_directory"));

            string linkedDir = Path.Combine(pathWebsite, "contents");
            string linkedDir2 = Path.Combine(pathWebsite, "contents2", "contents");

            UtilsSystem.EnsureDirectoryExists(linkedDir);
            UtilsSystem.EnsureDirectoryExists(linkedDir2);

            UtilsJunction.EnsureLink(linkedDir, pathContentsPersistent, logger, true, linkType: UtilsJunction.LinkTypeRequest.Junction);
            UtilsJunction.EnsureLink(linkedDir2, pathContentsPersistent, logger, true, linkType: UtilsJunction.LinkTypeRequest.Symlink);

            Assert.True(UtilsJunction.IsJunctionOrSymlink(linkedDir));
            Assert.True(UtilsJunction.IsJunctionOrSymlink(linkedDir2));

            string fileInContentsPeristent = Path.Combine(pathContentsPersistent, "test.txt");
            string fileInSymlinkDir = Path.Combine(linkedDir, "test.txt");

            Assert.Equal(fileInContentsPeristent, UtilsJunction.ResolvePath(fileInSymlinkDir));

            string fileInContentsPeristent2 = Path.Combine(pathContentsPersistent, "test2.txt");
            string fileInSymlinkDir2 = Path.Combine(linkedDir2, "test2.txt");

            Assert.Equal(fileInContentsPeristent2, UtilsJunction.ResolvePath(fileInSymlinkDir2));

            File.WriteAllText(fileInSymlinkDir, "testfile");
            File.WriteAllText(fileInSymlinkDir2, "testfile");

            Assert.True(File.Exists(fileInSymlinkDir), $"File exists {fileInSymlinkDir}");
            Assert.True(File.Exists(fileInSymlinkDir2), $"File exists {fileInSymlinkDir2}");
            Assert.True(File.Exists(fileInContentsPeristent), $"File exists {fileInContentsPeristent}");
            Assert.True(File.Exists(fileInContentsPeristent2), $"File exists {fileInContentsPeristent2}");

            // If we delete the directory containing the symlink, the file still exists
            UtilsSystem.DeleteDirectory(pathWebsite, logger);
            Assert.False(Directory.Exists(pathWebsite), "Directory exists " + pathWebsite);

            Assert.False(File.Exists(fileInSymlinkDir), $"File exists {fileInSymlinkDir}");
            Assert.True(File.Exists(fileInContentsPeristent), $"File exists {fileInContentsPeristent}");
            Assert.False(File.Exists(fileInSymlinkDir2), $"File exists {fileInSymlinkDir2}");
            Assert.True(File.Exists(fileInContentsPeristent2), $"File exists {fileInContentsPeristent2}");

            Directory.Delete(testPath, true);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestDirectoryDeletionDoesNotAffectSymlinks()
        {
            var logger = new MemoryLogger();

            var path1 = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            var fileInPath1 = Path.Combine(path1.FullName, Guid.NewGuid().ToString() + ".txt");
            File.WriteAllText(fileInPath1, string.Empty);

            var path2 = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            var path3 = Directory.CreateDirectory(
                Path.Combine(path2.FullName, Guid.NewGuid().ToString()));

            // Create a link in path2 to path1
            var pathLink = Path.Combine(path2.FullName, Guid.NewGuid().ToString());

            UtilsJunction.EnsureLink(pathLink, path1.FullName, logger, false);

            UtilsSystem.DeleteDirectoryAndCloseProcesses(path2.FullName, logger, null);

            // Nothing linked through a junction has been deleted
            Assert.True(path1.Exists);
            Assert.True(File.Exists(fileInPath1));

            // The actual directory has been deleted
            Assert.False(path2.Exists);
            Assert.False(path3.Exists);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestDirectoryDeletionDoesNotDeleteRootSymlink()
        {
            var logger = new MemoryLogger();

            var path1 = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            var fileInPath1 = Path.Combine(path1.FullName, Guid.NewGuid().ToString() + ".txt");
            File.WriteAllText(fileInPath1, string.Empty);

            var path2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            UtilsJunction.EnsureLink(path2, path1.FullName, logger, false);
            UtilsSystem.DeleteDirectoryAndCloseProcesses(path2, logger, null);

            // Nothing linked through a junction has been deleted
            Assert.True(path1.Exists);
            Assert.True(File.Exists(fileInPath1));

            // The actual directory has been deleted
            Assert.False(Directory.Exists(path2));
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestLongPathSupport()
        {
            string uncPath = "\\\\serverxx\\directory";
            string uncPathLong = UtilsSystem.AddLongPathSupport(uncPath);
            Assert.Equal("\\\\?\\UNC\\serverxx\\directory", uncPathLong);
            Assert.Equal(uncPath, UtilsSystem.RemoveLongPathSupport(uncPathLong));
            Assert.Equal(uncPath, UtilsSystem.RemoveLongPathSupport(uncPath));

            string regularPath = "c:\\windows\\temp";
            string regularPathLong = UtilsSystem.AddLongPathSupport(regularPath);
            Assert.Equal("\\\\?\\c:\\windows\\temp", regularPathLong);
            Assert.Equal(regularPath, UtilsSystem.RemoveLongPathSupport(regularPathLong));
            Assert.Equal(regularPath, UtilsSystem.RemoveLongPathSupport(regularPath));

            // Create a very long filename, individual segments can't be over 255 characters
            string fileName = "c:\\";
            for (int x = 0; x < 100; x++)
            {
                fileName += Guid.NewGuid() + "\\";
            }

            Assert.ThrowsAny<Exception>(() =>
            {
                Directory.CreateDirectory(fileName);
            });

            var fileNameWithLongPathSupport = UtilsSystem.EnsureLongPathSupportIfAvailable(fileName);

            Directory.CreateDirectory(fileNameWithLongPathSupport);

            fileNameWithLongPathSupport += "info.txt";

            File.WriteAllText(fileNameWithLongPathSupport, "empty contents");

            File.Delete(fileNameWithLongPathSupport);
        }

        /// <summary>
        /// Test CDN redirection
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestGetMaxWebConfigFileSizeInKb()
        {
            UtilsIis.GetMaxWebConfigFileSizeInKb();
        }
    }
}
