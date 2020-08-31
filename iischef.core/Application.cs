using iischef.core.Configuration;
using iischef.core.Exceptions;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Sql;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace iischef.core
{
    /// <summary>
    /// This is the main application class for the service.
    /// 
    /// Designed to be run in state-less mode. Starts, does what it has to do,
    /// and get's killed.
    /// </summary>
    public class Application
    {
        /// <summary>
        /// This prefix cannot be changed, as it will affect cleanup tasks in currently
        /// existing environments
        /// </summary>
        public const string AutoDeployApplicationIdPrefix = "auto-";

        /// <summary>
        /// The global settings
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public EnvironmentSettings GetGlobalSettings()
        {
            return this.GlobalSettings;
        }

        public ILoggerInterface GetLogger()
        {
            return this.Logger;
        }

        /// <summary>
        /// Get an instance of Application
        /// </summary>
        /// <param name="parentLogger">Logger implementation</param>
        public Application(ILoggerInterface parentLogger)
        {
            NewRelic.Api.Agent.NewRelic.SetApplicationName("IisChef");

            NewRelicAgentExtensions.AddCustomParameter("server", Environment.MachineName);
            NewRelicAgentExtensions.AddCustomParameter("user", Environment.UserName);

            BindingRedirectHandler.DoBindingRedirects(AppDomain.CurrentDomain);

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;

            // Check current account
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);

            parentLogger.LogInfo(false, $"Chef app started with identity '{identity.Name}'");

            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                parentLogger.LogError("Not running under full admin privileges.");

                if (Debugger.IsAttached)
                {
                    throw new Exception("You must run the deployer with full privileges.");
                }
            }

            // Use the parent logger, at least until we can build a file based one...
            this.Logger = parentLogger;
        }

        public ApplicationDeployer GetDeployer(InstalledApplication app)
        {
            return new ApplicationDeployer(this.GlobalSettings, app, this.Logger);
        }

        public Deployment DeploySingleAppFromInstalledApplication(InstalledApplication installedApplication, bool force = false, string buildId = null, bool sync = false)
        {
            var deployer = this.GetDeployer(installedApplication);
            var deployment = deployer.DeployApp(this, force, buildId, sync);
            return deployment;
        }

        public Deployment SyncSingleAppFromInstalledApplication(InstalledApplication installedApplication)
        {
            var deployer = this.GetDeployer(installedApplication);
            var deployment = deployer.SyncApp();
            return deployment;
        }

        /// <summary>
        /// Deploy a single app from it's YAML settings. For testing purposes only.
        /// </summary>
        /// <param name="settings"></param>
        public Deployment DeploySingleAppFromTextSettings(string settings, bool force = false, string buildId = null)
        {
            InstalledApplication installedApplication = new InstalledApplication();
            installedApplication.ParseFromString(settings);
            return this.DeploySingleAppFromInstalledApplication(installedApplication, force, buildId, true);
        }

        /// <summary>
        /// Undeploy an application by it's id...
        /// </summary>
        /// <param name="id"></param>
        /// <param name="force"></param>
        public void RemoveAppById(string id, bool force = false)
        {
            var deployment = ApplicationDeployer.GetActiveDeploymentById(this.GlobalSettings, id);
            if (deployment == null)
            {
                this.Logger.LogWarning(false, "Application not installed: " + id);
                return;
            }

            var appdeployer = new ApplicationDeployer(this.GlobalSettings, deployment.installedApplicationSettings, this.Logger);
            appdeployer.UninstallApp(force);
        }

        public void RestartAppById(string id)
        {
            var deployment = ApplicationDeployer.GetActiveDeploymentById(this.GlobalSettings, id);
            if (deployment == null)
            {
                this.Logger.LogWarning(false, "Application not installed: " + id);
                return;
            }

            var appdeployer = new ApplicationDeployer(this.GlobalSettings, deployment.installedApplicationSettings, this.Logger);
            appdeployer.RestartApp();
        }

        public void StartAppById(string id)
        {
            var deployment = ApplicationDeployer.GetActiveDeploymentById(this.GlobalSettings, id);
            if (deployment == null)
            {
                this.Logger.LogWarning(false, "Application not installed: " + id);
                return;
            }

            var appdeployer = new ApplicationDeployer(this.GlobalSettings, deployment.installedApplicationSettings, this.Logger);
            appdeployer.StartApp();
        }

        public void StopAppById(string id)
        {
            var deployment = ApplicationDeployer.GetActiveDeploymentById(this.GlobalSettings, id);
            if (deployment == null)
            {
                this.Logger.LogWarning(false, "Application not installed: " + id);
                return;
            }

            var appdeployer = new ApplicationDeployer(this.GlobalSettings, deployment.installedApplicationSettings, this.Logger);
            appdeployer.StopApp();
        }

        /// <summary>
        /// Deploy a single app from it's YAML settings. For testing purposes only.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="force"></param>
        public void DeploySingleAppFromFile(string path, bool force = false)
        {
            this.DeploySingleAppFromTextSettings(File.ReadAllText(path), force);
        }

        /// <summary>
        /// Undeploy a single app from it's YAML settings. For testing purposes only.
        /// </summary>
        /// <param name="settings"></param>
        public void UndeploySingleApp(string settings)
        {
            InstalledApplication app = new InstalledApplication();
            app.ParseFromString(settings);

            ApplicationDeployer deployer = new ApplicationDeployer(this.GlobalSettings, app, this.Logger);
            deployer.UninstallApp();
        }

        /// <summary>
        /// Get a single installed app
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public InstalledApplication GetInstalledApp(string id)
        {
            var apps = this.GetInstalledApps();
            return apps.FirstOrDefault(i => i.GetId() == id);
        }

        /// <summary>
        /// Get a list of all currently installed applications.
        /// </summary>
        /// <returns></returns>
        public List<InstalledApplication> GetInstalledApps(string identifiers = null)
        {
            List<InstalledApplication> apps = new List<InstalledApplication>();

            string activeDeploymentPathStorage = this.GlobalSettings.activeDeploymentDir;

            if (!Directory.Exists(activeDeploymentPathStorage))
            {
                throw new Exception("Active deployment path not found: " + activeDeploymentPathStorage);
            }

            foreach (var f in (new DirectoryInfo(activeDeploymentPathStorage)).EnumerateFiles("active.*.json"))
            {
                Deployment deployment = Deployment.InstanceFromPath(f.FullName, this.GlobalSettings);

                if (string.IsNullOrWhiteSpace(identifiers) ||
                    this.ExplodeAndCleanList(identifiers, ",").Contains(deployment.installedApplicationSettings.GetId()))
                {
                    apps.Add(deployment.installedApplicationSettings);
                }
            }

            return apps;
        }

        /// <summary>
        /// Expand a comma separated list of values
        /// </summary>
        /// <param name="list"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public List<string> ExplodeAndCleanList(string list, string pattern)
        {
            if (string.IsNullOrWhiteSpace(list))
            {
                return null;
            }

            return Regex.Split(list, pattern, RegexOptions.IgnoreCase).Select((i) => i.Trim()).Where((i) => !string.IsNullOrWhiteSpace(i)).ToList();
        }

        /// <summary>
        /// Get list of installed application templates.
        /// </summary>
        /// <returns></returns>
        public List<InstalledApplication> GetInstalledApplicationTemplates(string identifiers = null)
        {
            List<InstalledApplication> apps = new List<InstalledApplication>();

            var installedAppsFolder = this.GlobalSettings.applicationTemplateDir;
            var difo = new DirectoryInfo(installedAppsFolder);

            if (!difo.Exists)
            {
                throw new Exception($"Non existent installed applications folder: {installedAppsFolder}");
            }

            foreach (var file in difo.EnumerateFiles())
            {
                var installedApp = new InstalledApplication();
                installedApp.ParseFromString(File.ReadAllText(file.FullName));

                if (string.IsNullOrWhiteSpace(identifiers) ||
                    this.ExplodeAndCleanList(identifiers, ",").Contains(installedApp.GetId()))
                {
                    apps.Add(installedApp);
                }
            }

            return apps;
        }

        /// <summary>
        /// Redeploy an already installed application
        /// </summary>
        /// <param name="fromtemplate">If settings should be grabbed from the installed templates directory</param>
        /// <param name="identifiers">The identifiers, separated by commas.</param>
        /// <param name="force">Force the deployment</param>
        /// <param name="buildId">BuildId</param>
        /// <param name="sync"></param>
        /// <param name="tags"></param>
        /// <param name="automatic">If this call has been triggered by an automated service</param>
        /// <returns></returns>
        public List<Deployment> RedeployInstalledApplication(
            bool fromtemplate = false,
            string identifiers = null,
            bool force = false,
            string buildId = null,
            bool sync = false,
            string tags = null,
            bool automatic = false)
        {
            var identifierList = this.ExplodeAndCleanList(identifiers, ",");

            if (identifierList?.Count != 1 && buildId != null)
            {
                throw new Exception("Build id is only supported for single application deployments.");
            }

            if (identifierList?.Count != 1 && !string.IsNullOrWhiteSpace(tags))
            {
                throw new Exception("Tags are only supported for single app deployments.");
            }

            var installedApplications = fromtemplate ? this.GetInstalledApplicationTemplates(identifiers) : this.GetInstalledApps(identifiers);

            this.Logger.LogInfo(true, "Deploying {0} applications: {1}", installedApplications.Count(), string.Join(",", installedApplications.Select((i) => i.GetId())));

            List<Deployment> result = new List<Deployment>();

            foreach (var iap in installedApplications)
            {
                // If this is an automatic redeployment call, and the application does not have a redeploy...
                // continue
                if (automatic && !iap.GetAutodeploy())
                {
                    this.Logger.LogInfo(true, "Skipping deployment for application {0} with autodeploy {1}", iap.GetId(), iap.GetAutodeploy().ToString());
                    continue;
                }

                var deploymentHasErrorlockFilePath = Path.Combine(this.GlobalSettings.GetDefaultApplicationStorage().path, "_deploy_error_lock" + iap.GetId() + ".lock");

                if (File.Exists(deploymentHasErrorlockFilePath))
                {
                    var minutesSinceLastError = (DateTime.UtcNow - new FileInfo(deploymentHasErrorlockFilePath).LastWriteTimeUtc).TotalMinutes;

                    if (minutesSinceLastError < 60 && force != true)
                    {
                        this.Logger.LogWarning(
                            false,
                            $"The application {iap.GetId()} has recently ({Math.Round(minutesSinceLastError)} minutes ago) had a deployment error and is now locked. Use the -Force flag to force deployment, or remove the lock file at: {deploymentHasErrorlockFilePath}");

                        continue;
                    }
                }

                bool hasError = false;

                try
                {
                    // Appends tags if any...
                    if (!string.IsNullOrWhiteSpace(tags))
                    {
                        iap.MergeTags(tags);
                    }

                    var installedApplication = this.DeploySingleAppFromInstalledApplication(iap, force, buildId, sync);

                    result.Add(installedApplication);

                    // Remove the error lock file if any, as we had a successful deployment
                    if (File.Exists(deploymentHasErrorlockFilePath))
                    {
                        File.Delete(deploymentHasErrorlockFilePath);
                    }
                }
                catch (AlreadyHandledException)
                {
                    // Do nothing, any message from this exception has already been logged
                    hasError = true;
                }
                catch (StopDeploymentException)
                {
                    // do nothing
                }
                catch (TransientErrorException transientException)
                {
                    this.Logger.LogWarning(false, "Transient error: " + transientException.Message);
                }
                catch (Exception e)
                {
                    this.Logger.LogException(new Exception($"Error deploying application {iap.GetId()}", e));
                    hasError = true;
                }

                if (hasError)
                {
                    // If an application has deployment errors, write a "lock" file to prevent
                    // redeployments as this probably needs human intervention
                    File.WriteAllText(deploymentHasErrorlockFilePath, string.Empty);
                }
            }

            return result;
        }

        /// <summary>
        /// Execute cleanup for all installed
        /// applications.
        /// </summary>
        public void ExecuteCleanup()
        {
            var installedApplications = this.GetInstalledApps();

            this.Logger.LogInfo(false, "Cleaning up {0} applications: {1}", installedApplications.Count(), string.Join(",", installedApplications.Select((i) => i.GetId())));

            foreach (var iap in installedApplications)
            {
                try
                {
                    var deployer = this.GetDeployer(iap);
                    deployer.CleanupApp();
                }
                catch (Exception e)
                {
                    this.Logger.LogException(new Exception(
                        $"Error cleanning up application {iap.GetId()}", e));
                }
            }
        }

        public void RunCron(string identifiers = null)
        {
            var installedApplications = this.GetInstalledApps(identifiers);

            this.Logger.LogInfo(true, "Running cron for {0} applications: {1}", installedApplications.Count(), string.Join(",", installedApplications.Select((i) => i.GetId())));

            foreach (var iap in installedApplications)
            {
                try
                {
                    var deployer = this.GetDeployer(iap);
                    deployer.RunCron();
                }
                catch (Exception e)
                {
                    this.Logger.LogException(new Exception(
                        $"Error running cron on application {iap.GetId()}", e));
                }
            }
        }

        /// <summary>
        /// Deploy SSL for a site
        /// </summary>
        /// <param name="identifiers"></param>
        /// <param name="force"></param>
        public void DeploySsl(string identifiers = null, bool force = false)
        {
            var installedApplications = this.GetInstalledApps(identifiers);

            this.Logger.LogInfo(true, "Running DeploySsl for {0} applications: {1}", installedApplications.Count(), string.Join(",", installedApplications.Select((i) => i.GetId())));

            foreach (var iap in installedApplications)
            {
                try
                {
                    var deployer = this.GetDeployer(iap);
                    deployer.DeploySsl(force);
                }
                catch (Exception e)
                {
                    this.Logger.LogException(new Exception(
                        $"Error running cron on application {iap.GetId()}", e));
                }
            }
        }

        public List<Deployment> SyncInstalledApplication(string id = null)
        {
            var installedApplications = this.GetInstalledApps();

            if (!string.IsNullOrWhiteSpace(id))
            {
                installedApplications = installedApplications.Where((i) => i.GetId() == id).Take(1).ToList();
            }

            this.Logger.LogInfo(false, "Sync {0} applications: {1}", installedApplications.Count(), string.Join(",", installedApplications.Select((i) => i.GetId())));

            List<Deployment> result = new List<Deployment>();

            foreach (var iap in installedApplications)
            {
                try
                {
                    result.Add(this.SyncSingleAppFromInstalledApplication(iap));
                }
                catch (Exception e)
                {
                    this.Logger.LogException(new Exception(
                        $"Error synchronize application {iap.GetId()}", e));
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        protected string GetGlobalStoragePath(string filename)
        {
            var environmentSettingsFile =
                UtilsSystem.EnsureDirectoryExists(
                    UtilsSystem.CombinePaths(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "iischef",
                "config",
                filename));

            return environmentSettingsFile;
        }

        /// <summary>
        /// Delete unused temp files...
        /// </summary>
        protected void CleanupTempDirectories()
        {
            var logPath = this.GlobalSettings.GetDefaultTempStorage().path;

            var files = Directory.EnumerateFiles(logPath, "*", SearchOption.AllDirectories);

            foreach (string f in files)
            {
                try
                {
                    FileInfo info = new FileInfo(f);

                    // Delete files that have not been touched in the last 6 months
                    if ((DateTime.Now - info.LastWriteTime).TotalDays > (30 * 6))
                    {
                        System.IO.File.Delete(f);
                        this.Logger.LogInfo(true, "Deleted temp file: {0}", info.FullName);
                    }
                }
                catch (Exception e)
                {
                    this.Logger.LogException(e);
                }
            }
        }

        /// <summary>
        /// It was extremely difficult to control this at an application level
        /// so we do a global cleanup. On environments with many deployments,
        /// this can grow big very fast.
        /// </summary>
        protected void CleanupNetFrameworkTempFiles()
        {
            ////string netBase = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), @"..\.."));
            ////string strTemp32 = string.Concat(netBase, @"\Framework\", RuntimeEnvironment.GetSystemVersion(), @"\Temporary ASP.NET Files");
            ////string strTemp64 = string.Concat(netBase, @"\Framework64\", RuntimeEnvironment.GetSystemVersion(), @"\Temporary ASP.NET Files");

            // This has been disabled because
            // deleting based on a timestamp is NOT reliable, and these files might belong
            // to active applications. IIS will fail to re-compile if these files start missing
            // all of a sudden unless you reset IIS.

            ////this.CleanUpDir(strTemp64);
            ////this.CleanUpDir(strTemp32);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir"></param>
        public void CleanUpDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }

            foreach (var difo in new DirectoryInfo(dir).EnumerateDirectories())
            {
                foreach (var appDifo in difo.EnumerateDirectories())
                {
                    try
                    {
                        // Delete folders older than 1 month
                        if ((DateTime.Now - appDifo.LastWriteTime).TotalDays > 30)
                        {
                            UtilsSystem.DeleteDirectory(appDifo.FullName, this.Logger);
                            this.Logger.LogInfo(true, "Deleted temp dir: {0}", appDifo.FullName);
                        }
                    }
                    catch (Exception e)
                    {
                        this.Logger.LogException(e);
                    }
                }
            }
        }

        /// <summary>
        /// Archive and cleanup the log directories...
        /// 
        /// TODO: This is hardcoded here... should be a true cron
        /// that each deployer implements the way they want...
        /// </summary>
        protected void CleanupLogDirectories()
        {
            var logPath = this.GlobalSettings.GetDefaultLogStorage().path;
            this.Logger.LogInfo(true, "Cleaning log directories at path: {0}", logPath);

            var files = Directory.EnumerateFiles(logPath, "*", SearchOption.AllDirectories);

            foreach (string f in files)
            {
                try
                {
                    FileInfo info = new FileInfo(f);

                    // Delete any files not touched within the last six months.
                    if ((DateTime.Now - info.LastWriteTime).TotalDays > (30 * 6)
                        && (info.FullName.EndsWith("_bak.zip") || info.Extension.ToLower() == ".log"
                                                                   || info.Extension.ToLower() == ".txt"))
                    {
                        File.Delete(f);
                        this.Logger.LogInfo(true, "Deleted log file: {0}", info.FullName);
                        continue;
                    }

                    // Zip any log files that are larger than 100Mb or
                    // have not been writen into in the last 30 days.
                    bool extensionCriteria = (info.Extension.ToLower() == ".log"
                                              || info.Extension.ToLower() == ".txt");

                    bool timeCriteria = (DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays > 30;
                    bool sizeCriteria = info.Length > 1024 * 1024 * 100;

                    if (extensionCriteria && (timeCriteria && sizeCriteria))
                    {
                        string name = info.FullName;
                        string extensionlessName = name.Replace(info.Extension, string.Empty);
                        string folderTemp = extensionlessName;

                        Directory.CreateDirectory(extensionlessName);

                        info.MoveTo(UtilsSystem.CombinePaths(extensionlessName, info.Name));
                        string diff = info.CreationTime.ToString("yyyyMMddHHmmss");
                        ZipFile.CreateFromDirectory(folderTemp, extensionlessName + "_" + diff + "_bak.zip");
                        Directory.Delete(folderTemp, true);

                        this.Logger.LogInfo(true, "Archived log file: {0}", info.FullName);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    this.Logger.LogException(e, EventLogEntryType.Warning);
                }
                catch (FileNotFoundException e)
                {
                    this.Logger.LogException(e, EventLogEntryType.Warning);
                }
                catch (Exception e)
                {
                    this.Logger.LogException(e, EventLogEntryType.Warning);
                }
            }
        }

        protected string GetGlobalStorageVariable(string key)
        {
            var path = this.GetGlobalStoragePath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            return File.ReadAllText(path);
        }

        protected void SetGlobalStorageVariable(string key, string value)
        {
            var path = this.GetGlobalStoragePath(key);
            File.WriteAllText(path, value);
        }

        /// <summary>
        /// Set the location of the global environment file path.
        /// </summary>
        /// <param name="path"></param>
        public void SetGlobalEnvironmentFilePath(string path)
        {
            if (!File.Exists(path))
            {
                throw new Exception("Environment file does not exist: " + path);
            }

            this.SetGlobalStorageVariable("environment-file-path", path);
        }

        protected ILoggerInterface parentLogger;

        public void UseParentLogger()
        {
            this.Logger = this.parentLogger;
        }

        /// <summary>
        /// Install chef to the specified drive
        /// </summary>
        /// <param name="installDir"></param>
        /// <param name="environmentId"></param>
        public void SelfInstall(string installDir, string environmentId)
        {
            if (string.IsNullOrWhiteSpace(environmentId))
            {
                environmentId = Environment.MachineName;
            }

            string settingsFile = null;

            // If chef is already installed, don't let us install!!
            var environmentSettingsFile = this.GetGlobalStorageVariable("environment-file-path");

            if (settingsFile != null)
            {
                environmentSettingsFile = settingsFile;
            }

            if (File.Exists(environmentSettingsFile))
            {
                this.Logger.LogWarning(false, "Chef already installed. Current config file in: " + environmentSettingsFile);
                return;
            }

            var autosettings = new EnvironmentSettings();

            autosettings.id = environmentId;

            autosettings.contentStorages = new List<StorageLocation>();
            autosettings.contentStorages.Add(new StorageLocation()
            {
                id = "default",
                path = Path.Combine(installDir, "_contents")
            });
            autosettings.primaryContentStorage = "default";

            autosettings.tempStorages = new List<StorageLocation>();
            autosettings.tempStorages.Add(new StorageLocation()
            {
                id = "default",
                path = Path.Combine(installDir, "_temp")
            });
            autosettings.primaryTempStorage = "default";

            autosettings.applicationStorages = new List<StorageLocation>();
            autosettings.applicationStorages.Add(new StorageLocation()
            {
                id = "default",
                path = Path.Combine(installDir, "_apps")
            });
            autosettings.primaryApplicationStorage = "default";

            autosettings.logStorages = new List<StorageLocation>();
            autosettings.logStorages.Add(new StorageLocation()
            {
                id = "default",
                path = Path.Combine(installDir, "_log")
            });
            autosettings.primaryLogStorage = "default";

            autosettings.defaultApplicationLimits = new ApplicationLimits();
            ApplicationLimits.PopulateDefaultsIfMissing(autosettings.defaultApplicationLimits);

            foreach (var p in autosettings.logStorages)
            {
                UtilsSystem.EnsureDir(p.path);
            }

            foreach (var p in autosettings.applicationStorages)
            {
                UtilsSystem.EnsureDir(p.path);
            }

            foreach (var p in autosettings.tempStorages)
            {
                UtilsSystem.EnsureDir(p.path);
            }

            foreach (var p in autosettings.contentStorages)
            {
                UtilsSystem.EnsureDir(p.path);
            }

            var settingsDir = Path.Combine(installDir, "_configuration");
            var settingsPath = Path.Combine(settingsDir, "config.json");

            autosettings.endpoints = new List<NetworkInterface>();
            autosettings.endpoints.Add(new NetworkInterface()
            {
                forcehosts = true,
                id = "local",
                ip = UtilsIis.LOCALHOST_ADDRESS
            });

            // Retrieve the enumerator instance and then the data.
            SqlDataSourceEnumerator instance = SqlDataSourceEnumerator.Instance;
            this.Logger.LogInfo(false, "Searching for local SQL Server instances...");
            System.Data.DataTable table = instance.GetDataSources();

            string sqlserverSuffix = string.Empty;

            foreach (System.Data.DataRow t in table.Rows)
            {
                string id = "default" + sqlserverSuffix;

                string serverName = t["ServerName"] as string;
                string instanceName = t["InstanceName"] as string;

                string serverString = serverName;
                if (!string.IsNullOrWhiteSpace(instanceName))
                {
                    serverString += "\\" + instanceName;
                }

                var connectionString = $"Server={serverString};Integrated Security = true;";

                autosettings.sqlServers = new List<SQLServer>();
                autosettings.sqlServers.Add(new SQLServer()
                {
                    id = id,
                    connectionString = connectionString
                });

                this.Logger.LogInfo(false, "Added server: {0}", connectionString);

                autosettings.primarySqlServer = id;
                sqlserverSuffix = autosettings.sqlServers.Count.ToString();
            }

            UtilsSystem.EnsureDir(settingsDir);
            string serialized = JsonConvert.SerializeObject(autosettings, Formatting.Indented);
            File.WriteAllText(settingsPath, serialized);

            this.SetGlobalEnvironmentFilePath(settingsPath);

            autosettings.installationSalt = Guid.NewGuid().ToString();

            this.Logger.LogInfo(true, "Find chef configuration file at: {0}", settingsPath);
        }

        /// <summary>
        /// Path to the environment settings folder
        /// </summary>
        /// <param name="settingsFile"></param>
        public void Initialize(string settingsFile = null, string options = null)
        {
            var environmentSettingsFile = this.GetGlobalStorageVariable("environment-file-path");

            if (settingsFile == null && !File.Exists(environmentSettingsFile))
            {
                throw new Exception("To start the deployer you need to provide a valid environment configuration file. The default location is: " + environmentSettingsFile);
            }

            if (settingsFile != null)
            {
                environmentSettingsFile = settingsFile;
            }

            var serverSettingsContent = File.ReadAllText(environmentSettingsFile);

            this.GlobalSettings = JsonConvert.DeserializeObject<EnvironmentSettings>(serverSettingsContent);

            // Ensure we have a salt
            if (string.IsNullOrWhiteSpace(this.GlobalSettings.installationSalt))
            {
                this.Logger.LogWarning(true, "Global parameter 'installationSalt' no defined, using default salt.");
                this.GlobalSettings.installationSalt = "default-salt";
            }

            // Initialize the settings directory
            if (string.IsNullOrEmpty(this.GlobalSettings.settingsDir))
            {
                this.GlobalSettings.settingsDir = Path.GetDirectoryName(environmentSettingsFile);
                this.Logger.LogInfo(true, "No 'settingsDir' directory specified. Using default: {0}", environmentSettingsFile);
            }

            // Active deployment directory
            if (string.IsNullOrWhiteSpace(this.GlobalSettings.activeDeploymentDir))
            {
                this.GlobalSettings.activeDeploymentDir = UtilsSystem.CombinePaths(this.GlobalSettings.settingsDir, "deployments");

                // Initialize storage
                if (!Directory.Exists(this.GlobalSettings.activeDeploymentDir))
                {
                    Directory.CreateDirectory(this.GlobalSettings.activeDeploymentDir);
                }

                this.Logger.LogInfo(true, "No 'activeDeploymentDir' directory specified. Using default: {0}", this.GlobalSettings.activeDeploymentDir);
            }

            // Template directory
            if (string.IsNullOrWhiteSpace(this.GlobalSettings.applicationTemplateDir))
            {
                this.GlobalSettings.applicationTemplateDir = UtilsSystem.CombinePaths(this.GlobalSettings.settingsDir, "installed_apps");

                // Initialize storage
                if (!Directory.Exists(this.GlobalSettings.applicationTemplateDir))
                {
                    Directory.CreateDirectory(this.GlobalSettings.applicationTemplateDir);
                }

                this.Logger.LogInfo(true, "No 'applicationTemplateDir' directory specified. Using default: {0}", this.GlobalSettings.applicationTemplateDir);
            }

            if (this.GlobalSettings.options == null)
            {
                this.GlobalSettings.options = new List<string>();
            }

            if (options != null)
            {
                foreach (var option in options.Split(",".ToCharArray()))
                {
                    if (!this.GlobalSettings.options.Contains(option))
                    {
                        this.GlobalSettings.options.Add(option);
                    }
                }
            }

            // Now move to a file based logger
            // and keep track of original logger.
            this.parentLogger = this.Logger;
            this.Logger = new FileLogger(UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultLogStorage().path, $"chef-application-{this.GlobalSettings.id}.log"));
        }

        /// <summary>
        /// Run the AppVeyor monitor.
        /// </summary>
        public void RunAppVeyorMonitor()
        {
            // Run all the monitors
            if (this.GlobalSettings.appVeyorMonitors == null)
            {
                this.Logger.LogInfo(true, $"No appveyor monitors found in global settings. Skipping.");
                return;
            }

            foreach (var monitorSettings in this.GlobalSettings.appVeyorMonitors)
            {
                var monitor = new AppVeyorMonitor.AppVeyorMonitor(monitorSettings, this, this.Logger);
                monitor.FindNewDeployments();
            }
        }

        /// <summary>
        /// Uninstall and delete any expired applications.
        /// </summary>
        public void RemoveExpiredApplications(double currentDateUtcUnixTime, string id = null)
        {
            string timespanFormat = "d\\d\\ hh\\h\\ mm\\m\\ ss\\s";

            var installedApplications = this.GetInstalledApps();

            if (!string.IsNullOrWhiteSpace(id))
            {
                installedApplications = installedApplications.Where((i) => i.GetId() == id).ToList();
            }

            this.Logger.LogInfo(true, "Total installed applications: {0}", installedApplications.Count);

            foreach (var iap in installedApplications)
            {
                var deployer = this.GetDeployer(iap);

                // Max 30 days ttl allowed for ad-hoc environments.
                long maxTtl = 24 * 30;

                double ttl = deployer.DeploymentActive?.installedApplicationSettings?.GetExpires() ?? 0;

                if (ttl <= 0)
                {
                    this.Logger.LogInfo(true, "Environment {0} does not expire", iap.GetId());
                    continue;
                }

                if (ttl > maxTtl)
                {
                    this.Logger.LogInfo(true, "Environment '{0}' ttl exceeds maximum allowed.", iap.GetId());
                    ttl = maxTtl;
                }

                var ttlTimeSpan = new TimeSpan(0, (int)ttl, 0, 0);

                this.Logger.LogInfo(true, "Environment '{0}' has a total Ttl of '{1}'", iap.GetId(), ttlTimeSpan.ToString(timespanFormat));

                // Just a double check and safeguard... make sure the appid starts "auto-" that
                // is the prefix given by the autodeployer.
                if (!iap.GetId().StartsWith(Application.AutoDeployApplicationIdPrefix))
                {
                    this.Logger.LogWarning(false, $"Skipping removal of expired application because of missing prefix '{Application.AutoDeployApplicationIdPrefix}'");
                    continue;
                }

                long deploymentDate = deployer.DeploymentActive.DeploymentUnixTime ?? 0;

                var remainingTime = new TimeSpan(0, 0, 0, (int)(deploymentDate + ttlTimeSpan.TotalSeconds - currentDateUtcUnixTime));

                this.Logger.LogInfo(true, "Environment expires in {0}[{1}]", (remainingTime < TimeSpan.Zero) ? "-" : string.Empty, remainingTime.ToString(timespanFormat));

                if (remainingTime.TotalSeconds > 0)
                {
                    continue;
                }

                deployer.UninstallApp(true);
            }
        }

        /// <summary>
        /// This is called by the Chef service at periodic intervals.
        /// 
        /// Use this as a sort of "cron" trigger.
        /// </summary>
        public void RunServiceLoop()
        {
            this.RunOperationWithLog(() =>
            {
                this.RunMaintenance();
            });

            this.RunOperationWithLog(() =>
            {
                this.RunCron();
            });
        }

        /// <summary>
        /// Runs a loop of deployment related tasks
        /// </summary>
        public void RunDeploymentLoop()
        {
            // Monitor for new appveyor applications
            this.RunOperationWithLog(() =>
            {
                this.RunAppVeyorMonitor();
            });

            // Remove expired automatica applications
            this.RunOperationWithLog(() =>
            {
                this.RemoveExpiredApplications(DateTime.UtcNow.ToUnixTimestamp());
            });

            // Check for new versions in already installed applications
            this.RunOperationWithLog(() =>
            {
                this.RedeployInstalledApplication(automatic: true);
            });
        }

        public void RunMaintenance()
        {
            this.CleanupLogDirectories();
            this.CleanupTempDirectories();
            this.CleanupNetFrameworkTempFiles();
        }

        public void RunOperationWithLog(Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception e)
            {
                this.Logger.LogException(e);
            }
        }
    }
}
