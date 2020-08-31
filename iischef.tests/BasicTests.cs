using iischef.core;
using iischef.core.IIS.AcmeProviders;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using Xunit;
using Xunit.Abstractions;
using Application = iischef.core.Application;

namespace healthmonitortests
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

        /// <summary>
        /// Test that a deployment from AppVeyor works
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestAppVeyorBasedDeployment()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestAppVeyorBasedDeployment));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest");

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app.yml");

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");

            app.DeploySingleAppFromTextSettings(installedAppConfiguration);
            app.UndeploySingleApp(installedAppConfiguration);
        }

        /// <summary>
        /// Test that a deployment with expiration expires properly
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestAppVeyorBasedDeploymentExpires()
        {
            const string appId = "auto-chef-appveyor-sample-expires";
            Assert.True(appId.StartsWith(Application.AutoDeployApplicationIdPrefix), "Emulates an automated deployment.");

            var logger = new TestLogsLogger(this, nameof(this.TestAppVeyorBasedDeploymentExpires));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest");

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-expires.yml");

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");

            var deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, true);

            List<string> storeDirectories = new List<string>();

            storeDirectories.Add(deployment.appPath);
            storeDirectories.Add(deployment.logPath);
            storeDirectories.Add(deployment.tempPath);
            storeDirectories.Add(deployment.tempPathSys);

            storeDirectories.Add(UtilsJunction.ResolvePath(deployment.appPath));
            storeDirectories.Add(UtilsJunction.ResolvePath(deployment.logPath));
            storeDirectories.Add(UtilsJunction.ResolvePath(deployment.tempPath));
            storeDirectories.Add(UtilsJunction.ResolvePath(deployment.tempPathSys));

            foreach (var directory in storeDirectories)
            {
                Assert.True(Directory.Exists(directory), "Directory does exist: " + directory);
            }

            // Should not be removed
            app.RemoveExpiredApplications(DateTime.UtcNow.ToUnixTimestamp());
            Assert.True(app.GetInstalledApp(appId) != null);

            // Should not be removed
            app.RemoveExpiredApplications(DateTime.UtcNow.AddHours(15).ToUnixTimestamp());
            Assert.True(app.GetInstalledApp(appId) != null);

            // Should be removed
            app.RemoveExpiredApplications(DateTime.UtcNow.AddHours(26).ToUnixTimestamp());
            Assert.True(app.GetInstalledApp(appId) == null);

            // Extra test... make sure that al related store directories are gone
            foreach (var directory in storeDirectories)
            {
                Assert.False(Directory.Exists(directory), "Directory does not exist: " + directory);
            }
        }

        protected void AssertStringDoesNotContains(string needle, string haystack)
        {
            Assert.False(
                haystack.Contains(needle),
                $"Expected to find '{needle}' in '{haystack}'");
        }

        protected void AssertStringContains(string needle, string haystack)
        {
            Assert.True(
                haystack.Contains(needle),
                $"Expected to find '{needle}' in '{haystack}'");
        }

        protected void AssertStringEquals(string str1, string str2)
        {
            Assert.True(
                str1 == str2,
                $"Expected to '{str1}' to equal '{str2}'");
        }

        /// <summary>
        /// Assert hat a URI call response has a specific text.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="doesContains"></param>
        /// <param name="doesNotContains"></param>
        /// <param name="httpStatusCode"></param>
        protected void AssertUriContains(string uri, List<string> doesContains, List<string> doesNotContains, HttpStatusCode httpStatusCode = HttpStatusCode.OK)
        {
            string response = UtilsSystem.DownloadUriAsText(uri, true, httpStatusCode);

            if (doesContains != null)
            {
                foreach (var c in doesContains)
                {
                    this.AssertStringContains(c, response);
                }
            }

            if (doesNotContains != null)
            {
                foreach (var c in doesNotContains)
                {
                    this.AssertStringDoesNotContains(c, response);
                }
            }
        }

        /// <summary>
        /// Reset IIS
        /// </summary>
        private static void DoIisReset()
        {
            Process iisReset = new Process();
            iisReset.StartInfo.FileName = "iisreset.exe";
            iisReset.StartInfo.RedirectStandardOutput = true;
            iisReset.StartInfo.UseShellExecute = false;
            iisReset.Start();
            iisReset.WaitForExit();
            System.Threading.Thread.Sleep(500);

            StartService("W3SVC", 5000);
            StartService("WAS", 5000);
        }

        /// <summary>
        /// Start a service
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="timeoutMilliseconds"></param>
        public static void StartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (Exception e)
            {
                // ...
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="timeoutMilliseconds"></param>
        public static void StopService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch (Exception e)
            {
                // ...
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="timeoutMilliseconds"></param>
        public static void RestartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                int millisec1 = Environment.TickCount;
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                // count the rest of the timeout
                int millisec2 = Environment.TickCount;
                timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (Exception e)
            {
                // ...
            }
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV1_Link()
        {
            this._TestDeployPhpRuntimeV1("link");
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV1_Copy()
        {
            this._TestDeployPhpRuntimeV1("copy");
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV1_Move()
        {
            this._TestDeployPhpRuntimeV1("move");
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV1_Original()
        {
            this._TestDeployPhpRuntimeV1("original");
        }

        /// <summary>
        /// El deployer de IIS tiene unos flaws históricos
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestBrokenIisRedeploy()
        {
            var mountstrategy = "link";

            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestBrokenIisRedeploy));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest1");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv1 = UtilsSystem.GetResourceFileAsPath("samples/chef.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv1, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);

            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = null;

            installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", mountstrategy);

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            // Test the custom application limits for this application
            Assert.Equal(5, deployment.GetApplicationLimits().FastCgiMaxInstances);
            Assert.Equal(40, deployment.GetApplicationLimits().IisPoolMaxCpuLimitPercent);
            Assert.Equal("Throttle", deployment.GetApplicationLimits().IisPoolCpuLimitAction);
            Assert.Equal(36700189, deployment.GetApplicationLimits().IisPoolMaxPrivateMemoryLimitKb);

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "Hello world!",
                        "deployment.custom.setting",
                        "mycustomserverleveloverride",
                        "my_custom_setting",
                        "services.sqlsrv.username",
                        "services.sqlsrv.password",
                        "services.sqlsrv.database",
                        "services.sqlsrv.host",
                        "\"deployment.artifact.branch\": \"mainbranch\"",
                        "\"deployment.artifact.commit_sha\": \"5a1efde8b452e7a385592aa98a93066aa20282e5\"",
                        "services.couchbase-cache.uri",
                        "services.couchbase-cache.bucket-name",
                        "services.couchbase-cache.bucket-password"
                    },
                    null);
            };

            Action<string> testContains2 = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "e:/whatisthis/whatitis",
                        "e:/whatisthis/whatitis/",
                        "preferred_prefix"
                    },
                    null);
            };

            try
            {
                testContains2("http://chef.testing.framework/config.php");

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                var settings = app.GetGlobalSettings();
                this.AssertStringEquals("myusername", settings.accounts.First().username);

                // Let's delete directly the IIS site. What will happen is that during redeploy
                // we will get a SiteId for the new deployment that is already in-use by 
                // the previous deployment, so we have a conflict there that needs to 
                // be dealt with.

                string siteName = deployment.getShortId(); // "chf_php-test";

                using (ServerManager manager = new ServerManager())
                {
                    // Deploy the site, ideally NO site should be found
                    // here as it indicates a failed previous deployment.
                    var site = (from p in manager.Sites
                                where p.Name == siteName
                                select p).SingleOrDefault();

                    UtilsIis.RemoveSite(site, manager, logger);
                    UtilsIis.CommitChanges(manager);
                }

                app.RedeployInstalledApplication(false, deployment.installedApplicationSettings.GetId(), true);

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());

                // If the folder was moved, nothing to delete...
                if (mountstrategy != "move")
                {
                    UtilsSystem.DeleteDirectory(dir, logger);
                }
            }
        }

        /// <summary>
        /// El deployer de IIS tiene unos flaws históricos
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestSiteOfflinePage()
        {
            var mountstrategy = "link";

            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestSiteOfflinePage));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest1");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv1 = UtilsSystem.GetResourceFileAsPath("samples/chef.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv1, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = null;

            installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", mountstrategy);

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            // Make sure that the offline site has AUTOSTART false!
            using (var sm = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(sm, "off_" + deployment.getShortId(), logger).Single();
                Assert.False(site.ServerAutoStart);
            }

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "Hello world!"
                    },
                    null);
            };

            Action<string> testContains2 = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "Maintenance"
                    },
                    null,
                    HttpStatusCode.ServiceUnavailable);
            };

            try
            {
                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                app.StopAppById(deployment.installedApplicationSettings.GetId());

                using (var sm = new ServerManager())
                {
                    var site = UtilsIis.FindSiteWithName(sm, deployment.getShortId(), logger).Single();
                    Assert.False(site.ServerAutoStart);
                    site = UtilsIis.FindSiteWithName(sm, "off_" + deployment.getShortId(), logger).Single();
                    Assert.True(site.ServerAutoStart);
                }

                testContains2("http://chef.testing.framework");
                testContains2("http://chef.testing.framework.private");
                testContains2("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains2("http://local.chefcdn.com/mytestapplicationdirectory/");

                app.StartAppById(deployment.installedApplicationSettings.GetId());

                using (var sm = new ServerManager())
                {
                    var site = UtilsIis.FindSiteWithName(sm, deployment.getShortId(), logger).Single();
                    Assert.True(site.ServerAutoStart);
                    site = UtilsIis.FindSiteWithName(sm, "off_" + deployment.getShortId(), logger).Single();
                    Assert.False(site.ServerAutoStart);
                }

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                app.StopAppById(deployment.installedApplicationSettings.GetId());

                using (var sm = new ServerManager())
                {
                    var site = UtilsIis.FindSiteWithName(sm, deployment.getShortId(), logger).Single();
                    Assert.False(site.ServerAutoStart);
                    site = UtilsIis.FindSiteWithName(sm, "off_" + deployment.getShortId(), logger).Single();
                    Assert.True(site.ServerAutoStart);
                }

                testContains2("http://chef.testing.framework");
                testContains2("http://chef.testing.framework.private");
                testContains2("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains2("http://local.chefcdn.com/mytestapplicationdirectory/");
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());

                // If the folder was moved, nothing to delete...
                if (mountstrategy != "move")
                {
                    UtilsSystem.DeleteDirectory(dir, logger);
                }
            }
        }

        protected void _TestDeployPhpRuntimeV1(string mountstrategy)
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this._TestDeployPhpRuntimeV1) + "_" + mountstrategy);

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest1");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv1 = UtilsSystem.GetResourceFileAsPath("samples/chef.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv1, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = null;

            installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", mountstrategy);

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "Hello world!",
                        "deployment.custom.setting",
                        "mycustomserverleveloverride",
                        "my_custom_setting",
                        "services.sqlsrv.username",
                        "services.sqlsrv.password",
                        "services.sqlsrv.database",
                        "services.sqlsrv.host",
                        "\"deployment.artifact.branch\": \"mainbranch\"",
                        "\"deployment.artifact.commit_sha\": \"5a1efde8b452e7a385592aa98a93066aa20282e5\"",
                        "services.couchbase-cache.uri",
                        "services.couchbase-cache.bucket-name",
                        "services.couchbase-cache.bucket-password"
                    },
                    null);
            };

            Action<string> testContains2 = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "e:/whatisthis/whatitis",
                        "e:/whatisthis/whatitis/",
                    },
                    null);
            };

            try
            {
                testContains2("http://chef.testing.framework/config.php");

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("https://chef.testing.framework.nexus.sabentis.com");

                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                var settings = app.GetGlobalSettings();
                this.AssertStringEquals("myusername", settings.accounts.First().username);

                // If you deploy an artifact with "move"... then the original artifact is lost
                // and you cannot redeploy. TODO: Store copies of these?
                if (mountstrategy != "move")
                {
                    deployment =
                        app.RedeployInstalledApplication(false, deployment.installedApplicationSettings.GetId(), true)
                            .Single();

                    testContains("http://chef.testing.framework");
                    testContains("http://chef.testing.framework.private");
                    testContains("https://chef.testing.framework.nexus.sabentis.com");

                    testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                    testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                    var deployer = app.GetDeployer(deployment.installedApplicationSettings);
                    deployer.RunCron();
                    deployer.CleanupApp();
                }
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());

                // If the folder was moved, nothing to delete...
                if (mountstrategy != "move")
                {
                    UtilsSystem.DeleteDirectory(dir, logger);
                }
            }
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV2()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestDeployPhpRuntimeV2));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2.yml");
            var chefv2Local = UtilsSystem.GetResourceFileAsPath("samples/chef__local.yml");
            var chefv2Local2 = UtilsSystem.GetResourceFileAsPath("samples/chef__local2.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);
            File.Copy(chefv2Local, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local.yml")));
            File.Copy(chefv2Local2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local2.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                       uri,
                       new List<string>()
                       {
                        "Hello world!",
                        "deployment.custom.setting",
                        "mycustomserverleveloverride",
                        "my_custom_setting",
                        "my_overriden_local_setting",
                        "services.sqlsrv.username",
                        "services.sqlsrv.password",
                        "services.sqlsrv.database",
                        "services.sqlsrv.host",
                        "services.couchbase-cache.uri",
                        "services.couchbase-cache.bucket-name",
                        "services.couchbase-cache.bucket-password",
                        "\"deployment.artifact.branch\": \"mainbranch\"",
                        "\"deployment.artifact.commit_sha\": \"5a1efde8b452e7a385592aa98a93066aa20282e5\""
                       },
                       new List<string>()
                       {
                        "my_overriden_local_setting2"
                       });
            };

            try
            {
                testContains("https://chef.testing.framework.nexus.sabentis.com");

                var settings = app.GetGlobalSettings();
                this.AssertStringEquals("myusername", settings.accounts.First().username);

                app.RedeployInstalledApplication(false, deployment.installedApplicationSettings.GetId(), true);

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                var logcontents = logger.GetLog();
                Assert.True(logcontents.Contains("Tried to override mount path (services.contents.mount.files.path) with a non-existent directory"));
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                UtilsSystem.DeleteDirectory(dir, logger);
            }

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);
            app.RemoveAppById(deployment.installedApplicationSettings.GetId());

            UtilsSystem.DeleteDirectory(dir, logger);
            File.Delete(tempSettings);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestIpRestrictions()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestIpRestrictions));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2.yml");
            var chefv2Local = UtilsSystem.GetResourceFileAsPath("samples/chef__local.yml");
            var chefv2Local2 = UtilsSystem.GetResourceFileAsPath("samples/chef__local2.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);
            File.Copy(chefv2Local, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local.yml")));
            File.Copy(chefv2Local2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local2.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            try
            {
                var headers = new Dictionary<string, string>();

                var settings = app.GetGlobalSettings();
                this.AssertStringEquals("myusername", settings.accounts.First().username);

                headers["X-Forwareded-For"] = "148.142.5.10";

                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.Forbidden);

                headers["X-Forwareded-For"] = "148.142.12.10";

                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);

                headers["X-Forwareded-For"] = "127.0.0.1, 148.142.12.10:8080";

                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK, headers);

                headers["X-Forwareded-For"] = "127.0.0.1, 145.85.25.4:443";

                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.OK);
                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.Forbidden);

                headers["X-Forwareded-For"] = "148.142.12.15";

                UtilsSystem.DownloadUriAsText("http://chef.testing.framework", true, HttpStatusCode.Forbidden, headers);
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                UtilsSystem.DeleteDirectory(dir, logger);
            }

            UtilsSystem.DeleteDirectory(dir, logger);
            File.Delete(tempSettings);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestCertificateRenewalLogic()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestCertificateRenewalLogic));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2.yml");
            var chefv2Local = UtilsSystem.GetResourceFileAsPath("samples/chef__local.yml");
            var chefv2Local2 = UtilsSystem.GetResourceFileAsPath("samples/chef__local2.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);
            File.Copy(chefv2Local, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local.yml")));
            File.Copy(chefv2Local2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local2.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            // Get rid of any existing certificate
            UtilsIis.FindCertificateInCentralCertificateStore("chef.testing.framework.nexus.sabentis.com", logger, out var certificatePath);

            if (File.Exists(certificatePath))
            {
                File.Delete(certificatePath);
            }

            DoIisReset();

            var deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                       "Hello world!",
                    },
                    new List<string>()
                    {
                        "my_overriden_local_setting2"
                    });
            };

            try
            {
                // Initiail HTTPS load
                UtilsIis.EnsureCertificateInCentralCertificateStoreIsRebound("chef.testing.framework.nexus.sabentis.com", logger);

                testContains("https://chef.testing.framework.nexus.sabentis.com");
                Assert.Contains("Generating self signed certificate.", logger.GetLog());

                logger.Clear();

                deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

                testContains("https://chef.testing.framework.nexus.sabentis.com");

                Assert.DoesNotContain("Generating self signed certificate.", logger.GetLog());
                Assert.Contains("skipping SSL provisioning", logger.GetLog());

                // There is a bug in IIS where if we update the certificate in the central store,
                // but do not reconfigure IIS's HTTPS bindings, it fails to bind to the certificate.
                UtilsIis.FindCertificateInCentralCertificateStore("chef.testing.framework.nexus.sabentis.com", logger, out certificatePath);
                File.Delete(certificatePath);

                // A cron loop will renew the certificate
                logger.Clear();
                UtilsIis.EnsureCertificateInCentralCertificateStoreIsRebound("chef.testing.framework.nexus.sabentis.com", logger);

                // Now we query the site and there is an ERROR, as there is no certificate...
                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    testContains("https://chef.testing.framework.nexus.sabentis.com");
                });

                Assert.Equal("-2146233079", ex.HResult.ToString());

                // This will issue a new certificate
                logger.Clear();
                app.RunCron(deployment.installedApplicationSettings.GetId());
                Assert.Contains("Generating self signed certificate.", logger.GetLog());
                testContains("https://chef.testing.framework.nexus.sabentis.com");

                // This will not issue a certificate
                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId());
                Assert.DoesNotContain("Generating self signed certificate.", logger.GetLog());
                testContains("https://chef.testing.framework.nexus.sabentis.com");

                // This will issue a new certificate
                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId(), true);
                Assert.DoesNotContain("A self-signed certificate is provisioned on first installation of an application", logger.GetLog());
                Assert.Contains("Generating self signed certificate.", logger.GetLog());
                testContains("https://chef.testing.framework.nexus.sabentis.com");

                // Let's now try to trigger the failed validation protection mechanism...
                Environment.SetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION", true.ToString());

                // Clear any existing certificate
                File.Delete(certificatePath);

                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId(), true);
                Assert.Contains("Challenge could not be validated", logger.GetLog());
                Assert.Contains("Unable to acquire certificate and site does not have a valid existing one, using self-signed fallback.", logger.GetLog());

                // During the last force attempt, a self-signed certificate was issued, so unless we "force" it will consider that
                // as a valid cert
                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId(), false);
                Assert.Contains("Next renewal attempt date not reached, skipping SSL provisioning.", logger.GetLog());

                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId(), true);
                Assert.Contains("Challenge could not be validated", logger.GetLog());
                Assert.DoesNotContain("Unable to acquire certificate and site does not have a valid existing one, using self-signed fallback.", logger.GetLog());

                // Ensure the limit block pops-ups
                logger.Clear();
                app.DeploySsl(deployment.installedApplicationSettings.GetId(), false);
                Assert.Contains("has reached the limit of two failed validations", logger.GetLog());
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                UtilsSystem.DeleteDirectoryAndCloseProcesses(dir, logger, UtilsSystem.DefaultProcessWhitelist);
                Environment.SetEnvironmentVariable("TEST_FAIL_CHALLENGE_VALIDATION", false.ToString());
            }
        }

        /// <summary>
        /// Test that linked contents are not deleted or lost
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestContentsNotDeleted()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestContentsNotDeleted));

            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-localzip.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", sampleArtifact);

            var deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, true);

            // Prepare some sample files that should survive redeploy
            var file1 = Path.Combine(deployment.runtimeSettings["services.contents.mount.files.path"], "contentfile.html");
            file1 = UtilsJunction.ResolvePath(file1);
            File.WriteAllText(file1, "the contents");

            var file2 = Path.Combine(deployment.runtimeSettings["deployment.tempPath"], "contentfile.html");
            file2 = UtilsJunction.ResolvePath(file2);
            File.WriteAllText(file2, "the contents 2");

            var file3 = Path.Combine(deployment.runtimeSettings["deployment.logPath"], "contentfile.html");
            file3 = UtilsJunction.ResolvePath(file3);
            File.WriteAllText(file3, "the contents 3");

            try
            {
                this.AssertUriContains("http://chef.testing.framework/sites/default/contentfile.html", new List<string>() { "the contents" }, null);
                Assert.True(File.Exists(file1), $"File exists {file1}");
                Assert.True(File.Exists(file2), $"File exists {file2}");
                Assert.True(File.Exists(file3), $"File exists {file3}");

                app.RedeployInstalledApplication(false, deployment.installedApplicationSettings.GetId(), true);

                this.AssertUriContains("http://chef.testing.framework/sites/default/contentfile.html", new List<string>() { "the contents" }, null);
                Assert.True(File.Exists(file1), $"File exists {file1}");
                Assert.True(File.Exists(file2), $"File exists {file2}");
                Assert.True(File.Exists(file3), $"File exists {file3}");
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
            }

            File.Delete(tempSettings);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployLocalZip()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestDeployLocalZip));

            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-localzip.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", sampleArtifact);

            Deployment deployment;

            // Deploy twice to check for correct management of failed fonts
            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);
            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            Action<string> testContains = (string uri) =>
            {
                this.AssertUriContains(
                    uri,
                    new List<string>()
                    {
                        "Hello world!",
                        "services.sqlsrv.username",
                        "services.sqlsrv.password",
                        "services.sqlsrv.database",
                        "services.sqlsrv.host",
                        "\"cdn.preferred_prefix\": \"https:\\/\\/external-url2\\/cdn_chef-appveyor-sample-localzip\\/\"",
                    },
                    new List<string>()
                    {
                        "my_overriden_local_setting2"
                    });
            };

            try
            {
                testContains("http://chef.testing.framework");
                testContains("http://mainbranch.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                // Test the canonical CDN
                testContains("http://local.chefcdn.com/cdn_chef-appveyor-sample-localzip/");

                // Test cache buster in the CDN
                testContains("http://local.chefcdn.com/cachebuster_1265489/cdn_chef-appveyor-sample-localzip/");

                Assert.ThrowsAny<Exception>(() =>
                {
                    testContains("http://local.chefcdn.com/cachebuster_sdg9/cdn_chef-appveyor-sample-localzip/");
                });

                var settings = app.GetGlobalSettings();
                this.AssertStringEquals("myusername", settings.accounts.First().username);

                app.RedeployInstalledApplication(false, deployment.installedApplicationSettings.GetId(), true);

                testContains("http://chef.testing.framework");
                testContains("http://chef.testing.framework.private");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory");
                testContains("http://local.chefcdn.com/mytestapplicationdirectory/");

                // Test the canonical CDN
                testContains("http://local.chefcdn.com/cdn_chef-appveyor-sample-localzip/");
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
            }

            File.Delete(tempSettings);

            // Check if the fonts was installed
            var fonts = new InstalledFontCollection();
            Assert.Contains(fonts.Families, o => o.Name.StartsWith("Montserrat"));
            Assert.Contains(fonts.Families, o => o.Name.StartsWith("Montserrat Light"));
            Assert.Contains(fonts.Families, o => o.Name.StartsWith("Montserrat Alternate"));
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestDeployPhpRuntimeV3()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestDeployPhpRuntimeV3));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2.yml");
            var chefv2Local = UtilsSystem.GetResourceFileAsPath("samples/chef__local.yml");
            var chefv2Local2 = UtilsSystem.GetResourceFileAsPath("samples/chef__local2.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);
            File.Copy(chefv2Local, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local.yml")));
            File.Copy(chefv2Local2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local2.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, false, "latest");
            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, false, "1.0");

            try
            {
                deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, true);
            }
            catch (Exception e)
            {
                Assert.True(e.Message.Contains("Deployment was skipped because previous deployment was a version-specific deployment"));
            }

            app.RemoveAppById(deployment.installedApplicationSettings.GetId());

            UtilsSystem.DeleteDirectory(dir, logger);
            File.Delete(tempSettings);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestGetHandleAndCloseHandles()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestGetHandleAndCloseHandles));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectoryAndCloseProcesses(dir, logger, UtilsSystem.DefaultProcessWhitelist);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2.yml");
            var chefv2Local = UtilsSystem.GetResourceFileAsPath("samples/chef__local.yml");
            var chefv2Local2 = UtilsSystem.GetResourceFileAsPath("samples/chef__local2.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);
            File.Copy(chefv2Local, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local.yml")));
            File.Copy(chefv2Local2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef__local2.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            var deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, false, "latest");

            try
            {
                // Make a request to the site to ensure it has started and a process locks it
                this.AssertUriContains("http://chef.testing.framework", new List<string>() { "Hello world" }, null);
                var processes = UtilsProcess.GetPathProcessesInfo(deployment.runtimePath, logger);

                var names = processes.Select((i) => i.ProcessName).ToList();
                this.TestOutputHelper.WriteLine("Found processes A {0}", string.Join(", ", names));

                Assert.Contains("w3wp.exe", names);
                Assert.Contains("php-cgi.exe", names);

                // Close the processes
                UtilsProcess.ClosePathProcesses(deployment.runtimePath, UtilsSystem.DefaultProcessWhitelist, logger);
                processes = UtilsProcess.GetPathProcessesInfo(deployment.runtimePath, logger);
                names = processes.Select((i) => i.ProcessName).ToList();
                this.TestOutputHelper.WriteLine("Found processes B {0}", string.Join(", ", names));
                Assert.Empty(processes);

                // Make a request to the site to ensure it has started and a process locks it
                this.AssertUriContains("http://chef.testing.framework", new List<string>() { "Hello world" }, null);
                processes = UtilsProcess.GetPathProcessesInfo(deployment.runtimePath, logger);
                names = processes.Select((i) => i.ProcessName).ToList();
                this.TestOutputHelper.WriteLine("Found processes A {0}", string.Join(", ", names));

                // Delete the application directory, process closing should be handled automatically
                UtilsSystem.DeleteDirectoryAndCloseProcesses(deployment.runtimePath, logger, UtilsSystem.DefaultProcessWhitelist);
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                UtilsSystem.DeleteDirectory(dir, logger);
                File.Delete(tempSettings);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestAcmeSharpProviderMock()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestAlreadyInstalledCertificate));
            var mock = new AcmeSharpProviderMock(logger, "www.mysampledomain.com");
            mock.DownloadCertificate("certificatexxx", "www.mydomain.com", null);
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestProvisionImportAndRemoveSelfSignedCertificate()
        {
            var logger = new TestLogsLogger(this, nameof(this.TestAlreadyInstalledCertificate));

            string friendlyName = "test-certificate-chef-TestProvisionImportAndRemoveSelfSignedCertificate";

            UtilsCertificate.RemoveCertificateFromLocalStoreByFriendlyName(friendlyName, out _);

            var tmpCertPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pfx");

            string authorityPfxPath = null; // Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pfx");

            UtilsCertificate.CreateSelfSignedCertificateAsPfx(
                "chef.testing.framework.sabentis.com",
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

        [Fact]
        [Trait("Category", "Core")]
        public void TestAlreadyInstalledCertificate()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestAlreadyInstalledCertificate));

            // Lo que hace es instalar una aplcación que utiliza un certificado local
            // con el friendlyname "test-certificate-chef" (Ver chefv2_certificate.yml)
            // el certificado debe estar disponible para el test...

            string friendlyName = "test-certificate-chef";
            UtilsCertificate.RemoveCertificateFromLocalStoreByFriendlyName(friendlyName, out _);

            var tmpCertPath = Path.GetTempFileName();

            UtilsCertificate.CreateSelfSignedCertificateAsPfx(
                "chef.testing.framework.sabentis.com",
                tmpCertPath,
                string.Empty,
                null,
                logger,
                15);

            // We need to persist the key set in order for this to be available in IIS
            var cert = new X509Certificate2(tmpCertPath, string.Empty, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                cert.FriendlyName = friendlyName;
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert); // where cert is an X509Certificate object
            }

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest2");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv2 = UtilsSystem.GetResourceFileAsPath("samples/chefv2_certificate.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv2, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "link");

            var deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration, false, "latest");

            this.AssertUriContains(
                "https://chef.testing.framework.sabentis.com",
                new List<string>() { "https:\\/\\/chef.testing.framework.sabentis.com:443" },
                new List<string>() { });

            app.RemoveAppById(deployment.installedApplicationSettings.GetId());

            UtilsCertificate.RemoveCertificateFromLocalStoreByFriendlyName(friendlyName, out _);

            UtilsSystem.DeleteDirectory(dir, logger);
            File.Delete(tempSettings);
        }

        /// <summary>
        /// Test the service locally and manually.
        /// </summary>
        [Fact]
        [Trait("Category", "Core")]
        public void TestServiceLocalManual()
        {
            return;
            /*
            var service = new ApplicationService();
            service.Start();

            while (true)
            {
                System.Threading.Thread.Sleep(600);
            }
            */
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestExceptionWhenCdNbindingWrong()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestExceptionWhenCdNbindingWrong));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest1");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv1 = UtilsSystem.GetResourceFileAsPath("samples/chefCDNBindings.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv1, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chefCDNBindings.yml")));

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = null;

            installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "copy");

            Deployment deployment = null;
            bool hasException = false;
            try
            {
                deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);
            }
            catch (Exception e)
            {
                hasException = true;
                this.AssertStringContains("cdn_mount", e.Message);
            }
            finally
            {
                if (!hasException)
                {
                    app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                }

                // If the folder was moved, nothing to delete...
                UtilsSystem.DeleteDirectoryAndCloseProcesses(dir, logger, UtilsSystem.DefaultProcessWhitelist);

                // Fail test if exception was not thrown
                this.AssertStringContains(hasException.ToString(), true.ToString());
            }
        }

        [Fact]
        [Trait("Category", "Core")]
        public void TestSync()
        {
            DoIisReset();

            var logger = new TestLogsLogger(this, nameof(this.TestSync));

            // Prepare a local artifact from path
            string dir = UtilsSystem.GetTempPath("iischeftest1");

            UtilsSystem.DeleteDirectory(dir, logger);

            var chefv1 = UtilsSystem.GetResourceFileAsPath("samples/chef.yml");
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            ZipFile.ExtractToDirectory(sampleArtifact, dir);
            File.Copy(chefv1, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")), true);

            // Let's grab the
            var tempSettings = UtilsSystem.EnsureDirectoryExists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\iischef\\server-settings.json");
            File.WriteAllText(tempSettings, UtilsSystem.GetResourceFileAsString("samples/server-settings.json"));

            var app = new Application(logger);
            app.Initialize(tempSettings, "testenvironment");
            app.UseParentLogger();

            string installedAppConfiguration = null;

            installedAppConfiguration = UtilsSystem.GetResourceFileAsString("samples/sample-app-local_sync_1.yml");
            installedAppConfiguration = installedAppConfiguration.Replace("%PATH%", dir);
            installedAppConfiguration = installedAppConfiguration.Replace("%MOUNTSTRATEGY%", "copy");

            Deployment deployment;

            deployment = app.DeploySingleAppFromTextSettings(installedAppConfiguration);

            foreach (var pathSync in System.IO.Directory.EnumerateDirectories(
                deployment.GetRuntimeSettingsToDeploy()["services." + "contents" + ".mount.files.path"],
                "*",
                SearchOption.AllDirectories))
            {
                File.Create(UtilsSystem.CombinePaths(pathSync, "sync_test.txt")).Close();
            }

            File.Create(UtilsSystem.CombinePaths(deployment.GetRuntimeSettingsToDeploy()["services." + "contents" + ".mount.files.path"], "sync_test.txt")).Close();

            foreach (var server in app.GetGlobalSettings().sqlServers)
            {
                SqlConnection conn = new SqlConnection(server.connectionString);
                conn.Open();
                conn.ChangeDatabase(deployment.GetRuntimeSettingsToDeploy()["services." + "sqlsrv" + ".database"]);
                SqlCommand cm00 = new SqlCommand("IF OBJECT_ID('dbo.a', 'U') IS NOT NULL DROP TABLE dbo.a;", conn);
                cm00.ExecuteNonQuery();
                SqlCommand cm0 = new SqlCommand("CREATE TABLE a (ID char(5) NOT NULL,PRIMARY KEY(ID));", conn);
                cm0.ExecuteNonQuery();
                SqlCommand cm1 = new SqlCommand("insert into a (id) VALUES ('hello');", conn);
                cm1.ExecuteNonQuery();
                conn.Close();
            }

            // Deploy son application
            string installedAppConfigurationSon = null;
            installedAppConfigurationSon = UtilsSystem.GetResourceFileAsString("samples/sample-app-local_sync_2.yml");
            installedAppConfigurationSon = installedAppConfigurationSon.Replace("%PATH%", dir);
            installedAppConfigurationSon = installedAppConfigurationSon.Replace("%MOUNTSTRATEGY%", "copy");

            var chefsync = UtilsSystem.GetResourceFileAsPath("samples/cheff_sync.yml");
            File.Delete(UtilsSystem.CombinePaths(dir, "chef", "chef.yml"));
            File.Copy(chefsync, UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(dir, "chef", "chef.yml")));

            Deployment deploymentSon;
            deploymentSon = app.DeploySingleAppFromTextSettings(installedAppConfigurationSon);

            try
            {
                foreach (var pathSync in System.IO.Directory.EnumerateDirectories(
                deploymentSon.GetRuntimeSettingsToDeploy()["services." + "contents" + ".mount.files.path"],
                "*",
                SearchOption.AllDirectories))
                {
                    Assert.True(
                        System.IO.Directory.GetFiles(pathSync).ToList()
                        .Exists(s => s.Contains("sync_test.txt")),
                        "There is NO sync file in " + pathSync);
                }

                Assert.True(
                    System.IO.Directory.GetFiles(
                    deploymentSon.GetRuntimeSettingsToDeploy()["services." + "contents" + ".mount.files.path"])
                    .ToList().Exists(s => s.Contains("sync_test.txt")), "There is NO sync file in Root");

                foreach (var server in app.GetGlobalSettings().sqlServers)
                {
                    SqlConnection conn = new SqlConnection(server.connectionString);
                    conn.Open();
                    conn.ChangeDatabase(deploymentSon.GetRuntimeSettingsToDeploy()["services." + "sqlsrv" + ".database"]);
                    SqlCommand cm = new SqlCommand("select id from a;", conn);
                    SqlDataReader reader = cm.ExecuteReader();
                    Assert.True(reader.HasRows, "The are NO rows");
                    reader.Read();
                    this.AssertStringEquals("hello", reader.GetString(reader.GetOrdinal("ID")));
                    conn.Close();
                }
            }
            finally
            {
                app.RemoveAppById(deployment.installedApplicationSettings.GetId());
                app.RemoveAppById(deploymentSon.installedApplicationSettings.GetId());
                UtilsSystem.DeleteDirectory(dir, logger);
            }
        }
    }
}