using iischef.core;
using iischef.core.IIS.AcmeProviders;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Xunit;

namespace healthmonitortests
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TestJsonKvConverter()
        {
            var converter = new iischef.core.Configuration.JObjectToKeyValueConverter();

            var original = JObject.FromObject(
                new
                {
                    mailSettings = new
                    {
                        host = "myhost",
                        port = "myport"
                    },
                    myArray = new List<object>()
                    {
                        "a",
                        "b",
                        "c",
                        new
                        {
                            pro1 = "prop1",
                            pro2 = "prop2",
                        }
                    }
                });

            var c1 = converter.NestedToKeyValue(original);

            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(c1);
            Assert.Equal(
                "{\"mailSettings.host\":\"myhost\",\"mailSettings.port\":\"myport\",\"myArray.0\":\"a\",\"myArray.1\":\"b\",\"myArray.2\":\"c\",\"myArray.3.pro1\":\"prop1\",\"myArray.3.pro2\":\"prop2\"}",
                serialized);

            // Now the other way round...
            JObject reverted = converter.keyValueToNested(c1);

            Assert.Equal(
                Newtonsoft.Json.JsonConvert.SerializeObject(original),
                Newtonsoft.Json.JsonConvert.SerializeObject(reverted));
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestIisGetConfigIsolationPath()
        {
            Assert.NotNull(UtilsIis.GetConfigIsolationPath());
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestSimpleStore()
        {
            var simpleStore = new SimpleStore(UtilsSystem.GetTempPath("samplestore"));
            simpleStore.Clear();

            Assert.False(simpleStore.Get<string>("itemA", out _));

            simpleStore.Set("itemA", "testdataA", 50);

            Assert.True(simpleStore.Get<string>("itemA", out var itemA));
            Assert.Equal("testdataA", itemA.Data);

            Assert.False(simpleStore.Get<string>("itemB", out _));

            simpleStore.Set("itemB", "testdataB", 50);

            Assert.True(simpleStore.Get<string>("itemA", out itemA));
            Assert.Equal("testdataA", itemA.Data);

            Assert.True(simpleStore.Get<string>("itemB", out var itemB));
            Assert.Equal("testdataB", itemB.Data);
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
        /// Test the service locally and manually.
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestSettingsReplacer()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            settings["a"] = "this thing has a backslash and needs to be \\ escaped to be used as a json literal";
            settings["b"] = "this thing has a <html> markup that needs ' to be put inside an XML \" placeholder to be & used as a json literal";

            var replacer = new RuntimeSettingsReplacer(settings);

            Assert.Equal("this thing has a backslash and needs to be \\\\ escaped to be used as a json literal", replacer.DoReplace("{@a|filter: jsonescape@}"));
            Assert.Equal("this thing has a backslash and needs to be \\\\ escaped to be used as a json literal", replacer.DoReplace("{@a|filter:jsonescape@}"));

            Assert.Equal("this thing has a backslash and needs to be / escaped to be used as a json literal", replacer.DoReplace("{@a|filter:allforward@}"));

            Assert.Equal("this thing has a &lt;html&gt; markup that needs &apos; to be put inside an XML &quot; placeholder to be &amp; used as a json literal", replacer.DoReplace("{@b|filter:xmlescape@}"));
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestWindowsRight()
        {
            ILoggerInterface logger = new TestLogsLogger(this, nameof(this.TestWindowsRight));

            UtilsWindowsAccounts.EnsureUserExists("chf_testaccount", "81Dentiaaeh#" + Guid.NewGuid(), "The display name", logger, null);
            UtilsWindowsAccounts.EnsureGroupExists("chef_testgroup", null);
            UtilsWindowsAccounts.EnsureUserInGroup("chf_testaccount", "chef_testgroup", logger, null);
            Assert.Equal(0, UtilsWindowsAccounts.SetRight("chf_testaccount", "SeCreateSymbolicLinkPrivilege", logger));
            Assert.Equal(0, UtilsWindowsAccounts.SetRight("chf_testaccount", "SeBatchLogonRight", logger));
            UtilsWindowsAccounts.DeleteUser("chf_testaccount", null);
        }

        /// <summary>
        /// Only for manual testing to debug the service.
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestServiceCanStop()
        {
            var service = new ApplicationService();
            service.Start();

            var start = DateTime.Now;

            while (true)
            {
                Thread.Sleep(400);

                if ((DateTime.Now - start).TotalSeconds > 2)
                {
                    service.Stop();
                    break;
                }
            }
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestAcmeVault()
        {
            AcmeSharpProvider.TestVault();
        }

        /// <summary>
        /// Converting PEM to PFX requires some 3d party libraries that brake every once in a while...
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void TestPfxFromPem()
        {
            BindingRedirectHandler.DoBindingRedirects(AppDomain.CurrentDomain);

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
        public void TestNameParser()
        {
            Assert.Equal("sabentis.loc", new FqdnNameParser("myname@sabentis.loc", false).DomainName);
            Assert.Equal("myname", new FqdnNameParser("myname@sabentis.loc", false).UserPrincipalName);
            Assert.Equal(ContextType.Domain, new FqdnNameParser("myname@sabentis.loc", false).ContextType);

            Assert.Equal("SABENTIS", new FqdnNameParser("SABENTIS\\myname", false).DomainName);
            Assert.Equal("myname", new FqdnNameParser("SABENTIS\\myname", false).UserPrincipalName);
            Assert.Equal(ContextType.Domain, new FqdnNameParser("SABENTIS\\myname", false).ContextType);

            Assert.Equal(ContextType.Machine, new FqdnNameParser("myname@localhost", false).ContextType);
            Assert.Equal(ContextType.Machine, new FqdnNameParser("LOCALHOST\\myname", false).ContextType);

            Assert.NotNull(new FqdnNameParser("S-1-5-32-559").Sid);
            Assert.NotNull(new FqdnNameParser("sid:S-1-5-32-559").Sid);
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
        public void TestCdnReplacements()
        {
            var sampleHtml = File.ReadAllText(UtilsSystem.GetResourceFileAsPath("samples/samplecdn.html"));

            var replacer = new CdnHtmlRedirectHelper();

            replacer.PrependCdnToUri(sampleHtml, "https://cdnprefix/directory/");
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

        /// <summary>
        /// Test than we can provision self-signed certificates, and that we can remove them
        /// </summary>
        [Fact]
        [Trait("Category", "Functional")]
        public void GenerateSampleApplicationSettings()
        {
            var settings = new EnvironmentSettings();

            settings.couchbaseServers = new List<CouchbaseServer>();
            settings.couchbaseServers.Add(new CouchbaseServer()
            {
                id = "default",
                uri = "couchbase://127.0.0.1",
                bucketName = "couch",
                bucketPassword = "couch"
            });

            settings.primaryCouchbaseServer = "default";

            settings.sqlServers = new List<SQLServer>();
            settings.sqlServers.Add(new SQLServer()
            {
                id = "default",
                connectionString = "Server=localhost;"
            });

            settings.primarySqlServer = "default";

            settings.contentStorages = new List<StorageLocation>();
            settings.contentStorages.Add(
                new StorageLocation()
                {
                    id = "default",
                    path = "D:\\_contents"
                });

            settings.primaryContentStorage = "default";

            settings.applicationStorages = new List<StorageLocation>();
            settings.applicationStorages.Add(
                new StorageLocation()
                {
                    id = "default",
                    path = "D:\\_webs"
                });

            settings.primaryApplicationStorage = "default";

            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
