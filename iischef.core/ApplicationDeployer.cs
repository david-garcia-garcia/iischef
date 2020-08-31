using FileLock;
using iischef.core.Configuration;
using iischef.core.Exceptions;
using iischef.core.IIS;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace iischef.core
{
    /// <summary>
    /// Component that manages full application deployments
    /// </summary>
    public class ApplicationDeployer
    {
        /// <summary>
        /// Environment settings
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        /// <summary>
        /// Current active deployment for this application
        /// </summary>
        public Deployment DeploymentActive;

        /// <summary>
        /// Where to store the active deployment settings
        /// </summary>
        private readonly string activeDeploymentPathStorage;

        /// <summary>
        /// Installed application settings.
        /// </summary>
        private readonly InstalledApplication installedAppSettings;

        /// <summary>
        /// 
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Return a list of active deployments in the system.
        /// </summary>
        /// <returns></returns>
        public static List<Deployment> GetActiveDeployments(EnvironmentSettings globalSettings)
        {
            List<Deployment> result = new List<Deployment>();
            DirectoryInfo dir = new DirectoryInfo(globalSettings.activeDeploymentDir);
            foreach (var f in dir.EnumerateFiles("active.*.json"))
            {
                result.Add(Deployment.InstanceFromPath(f.FullName, globalSettings));
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="globalSettings"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Deployment GetActiveDeploymentById(EnvironmentSettings globalSettings, string id)
        {
            List<Deployment> result = new List<Deployment>();
            DirectoryInfo dir = new DirectoryInfo(globalSettings.activeDeploymentDir);
            foreach (var f in dir.EnumerateFiles("active." + id + ".json"))
            {
                return Deployment.InstanceFromPath(f.FullName, globalSettings);
            }

            return null;
        }

        /// <summary>
        /// Get a deployer for the installed application.
        /// </summary>
        /// <param name="globalSettings">The global settings.</param>
        /// <param name="installedApplicationSettings">The installed application settings.</param>
        /// <param name="logger">The logger.</param>
        public ApplicationDeployer(
            EnvironmentSettings globalSettings,
            InstalledApplication installedApplicationSettings,
            ILoggerInterface logger)
        {
            this.GlobalSettings = globalSettings;
            this.installedAppSettings = installedApplicationSettings;
            this.Logger = logger;

            if (this.GlobalSettings == null)
            {
                throw new InvalidDataException("settings argument cannot be null.");
            }

            if (this.installedAppSettings == null)
            {
                throw new Exception("installedApplicationSettings argument cannot be null.");
            }

            // Try to grab previous deployment...
            this.activeDeploymentPathStorage = UtilsSystem.CombinePaths(globalSettings.activeDeploymentDir, "active." + this.installedAppSettings.GetId() + ".json");

            if (File.Exists(this.activeDeploymentPathStorage))
            {
                this.DeploymentActive = Deployment.InstanceFromPath(this.activeDeploymentPathStorage, globalSettings);
            }
        }

        /// <summary>
        /// Uninstalls and APP and removes all services and deployments.
        /// </summary>
        /// <param name="force">If set to true, will force removal even if there are partial failures.</param>
        public void UninstallApp(bool force = true)
        {
            this.Logger.LogInfo(false, "UninstallApp: " + this.DeploymentActive.getShortId());

            DeployerCollection deployers = null;
            DeployerCollection services = null;

            try
            {
                deployers = this.DeploymentActive.GrabDeployers(this.Logger);
            }
            catch (Exception e0)
            {
                if (!force)
                {
                    throw;
                }
            }

            try
            {
                services = this.DeploymentActive.GrabServices(this.Logger);
            }
            catch (Exception e0)
            {
                if (!force)
                {
                    throw;
                }
            }

            try
            {
                // We need to stop prior to removing...
                deployers?.StopAll();
                services?.StopAll();

                // Cleanup previous environments
                deployers?.UndeployAll(true, true);
                services?.UndeployAll(true, true);

                // Remove
                File.Delete(this.activeDeploymentPathStorage);
            }
            catch (Exception e)
            {
                if (force)
                {
                    this.Logger.LogException(new Exception("An issue was found during application removal. But still application has been uninstalled by using the -force option.", e));
                    File.Delete(this.activeDeploymentPathStorage);
                }
                else
                {
                    throw;
                }
            }
        }

        protected void AddConfigurationFileToMergeChain(ApplicationSettings s, List<ApplicationSettings> mergeChain, List<FileInfo> files, int depth)
        {
            if (depth > 4)
            {
                throw new Exception("Maximum inheritance depth reached, posible recursion.");
            }

            this.Logger.LogInfo(false, "Merging settings file: " + s.getSourcePath());

            // Check inheritance, you can inherit from another file or use a break (stop inheritance)
            string inheritance = s.GetInherit();

            if (string.IsNullOrWhiteSpace(inheritance))
            {
                mergeChain.Add(s);
            }
            else if (inheritance == "break")
            {
                this.Logger.LogInfo(false, "Settings file requested an inheritance break. Loosing all previous settings.");
                mergeChain.Clear();
                mergeChain.Add(s);
            }
            else if (files.Any((f) => f.Name.Equals(inheritance, StringComparison.CurrentCultureIgnoreCase)))
            {
                var inheritFrom = files.First((f) =>
                    f.Name.Equals(inheritance, StringComparison.CurrentCultureIgnoreCase));

                this.Logger.LogInfo(true, "Inherit from: {0}", inheritFrom.Name);

                var newBaseConfig = new ApplicationSettings();
                newBaseConfig.ParseFromFile(inheritFrom.FullName);

                this.AddConfigurationFileToMergeChain(newBaseConfig, mergeChain, files, depth + 1);

                mergeChain.Add(s);
            }
            else
            {
                throw new Exception($"Could not resolve inheritance {inheritance}.");
            }
        }

        /// <summary>
        /// Build the final chef.yml configuration file
        /// </summary>
        /// <param name="appPath">Path to the chef folder</param>
        /// <param name="environmentId">Environment id</param>
        /// <param name="branch">Branch name</param>
        /// <param name="usedConfigFiles">List of configuration files merged to generate the final configuration</param>
        /// <returns></returns>
        protected ApplicationSettings LoadApplicationSettings(
            string appPath,
            string environmentId,
            string branch,
            out List<string> usedConfigFiles)
        {
            // Ensure environment and branch are not null, to prevent
            // Regex.Match from crashing.
            if (environmentId == null)
            {
                environmentId = string.Empty;
            }

            if (branch == null)
            {
                branch = string.Empty;
            }

            // A little bit shitty... but we need to parse ALL configuration files
            // to figure out which one applies to current branch and/or environment.
            DirectoryInfo chefConfigFilesDirectory = new DirectoryInfo(appPath);

            if (!chefConfigFilesDirectory.Exists)
            {
                throw new Exception("The provided application path is missing a chef folder: " + appPath);
            }

            var files = chefConfigFilesDirectory.EnumerateFiles("chef*.yml").ToList();

            List<ApplicationSettings> configurationFiles = new List<ApplicationSettings>();

            var environmentTags = this.GlobalSettings.getOptions();
            environmentTags.AddRange(this.installedAppSettings.GetTags());

            this.Logger.LogInfo(true, "Effective environment tags: " + string.Join(", ", environmentTags));

            // We look for all matching configuration files, and do a weight based merge.
            foreach (var f in files)
            {
                var config = new ApplicationSettings();
                config.ParseFromFile(f.FullName);

                bool environmentMatch = Regex.IsMatch(environmentId, config.getScopeEnvironmentRegex());
                bool branchMatch = Regex.IsMatch(branch, config.GetScopeBranchRegex());
                bool optionsMatch = (!config.GetScopeTags().Any())
                    || environmentTags.Intersect(config.GetScopeTags()).Any();

                if (environmentMatch && branchMatch && optionsMatch)
                {
                    configurationFiles.Add(config);
                }
            }

            if (!configurationFiles.Any())
            {
                throw new Exception("Could not find a suitable chef*.yml file for the current environment in: " + chefConfigFilesDirectory.FullName);
            }

            usedConfigFiles = new List<string>();

            // We now are going to MERGE all settings. Careful here, because order does MATTER when doing overrides.
            // Ideally this would be done with regex specifity, but that is to tough. Just use weights :)
            // http://stackoverflow.com/questions/3611860/determine-regular-expressions-specificity
            configurationFiles = configurationFiles.OrderBy((i) => i.getScopeWeight()).ToList();

            // Now merge all into the first one...
            List<ApplicationSettings> mergeChain = new List<ApplicationSettings>();

            foreach (var s in configurationFiles)
            {
                this.AddConfigurationFileToMergeChain(s, mergeChain, files, 0);
            }

            // Now merge the settings file
            this.Logger.LogInfo(true, "Merging settings file: {0}", string.Join(", ", mergeChain.Select((i) => Path.GetFileName(i.getSourcePath()))));

            ApplicationSettings finalConfiguration = null;

            foreach (var m in mergeChain)
            {
                usedConfigFiles.Add(m.getSourcePath());

                if (finalConfiguration == null)
                {
                    finalConfiguration = m;
                    continue;
                }

                finalConfiguration.Merge(m);
            }

            return finalConfiguration;
        }

        /// <summary>
        /// Do not deploy applications if we are short on disk space.
        ///
        /// Throws an exception if size is below minSize
        /// </summary>
        /// <param name="minSize">Defaults to 500Mb</param>
        protected void CheckDiskSpace(long minSize = 524288000)
        {
            var applicationPath = this.GlobalSettings.GetDefaultApplicationStorage().path;

            long freeSpaceBytes = UtilsSystem.GetTotalFreeSpace(applicationPath);

            if (freeSpaceBytes < minSize)
            {
                throw new Exception($"Insuficient storage [{UtilsSystem.BytesToString(freeSpaceBytes)}] to run deployments in: {applicationPath}");
            }
        }

        /// <summary>
        /// A file name that will be used for physical file locks
        /// </summary>
        /// <returns></returns>
        protected string LockPathForApplication()
        {
            return UtilsSystem.EnsureDirectoryExists(
                UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultApplicationStorage().path, "_chef_locks", this.GlobalSettings.id, "application." + this.installedAppSettings.GetId() + ".lock"));
        }

        /// <summary>
        /// Deploy an application
        /// </summary>
        /// <param name="app"></param>
        /// <param name="force"></param>
        /// <param name="buildId"></param>
        /// <param name="sync"></param>
        /// <returns></returns>
        public Deployment DeployApp(Application app, bool force = false, string buildId = null, bool sync = false)
        {
            this.CheckDiskSpace();
            return this.RunActionIfApplicationNotLocked(() => this._DeployApp(app, force, buildId, sync), $"Deploy application {this.installedAppSettings.GetId()}");
        }

        /// <summary>
        /// Run an action only if this application is not locked
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        protected T RunActionIfApplicationNotLocked<T>(Func<T> a, string operation)
        {
            // Don't let an application be deployed in parallel, could lead to breaks!
            string lockPath = this.LockPathForApplication();
            var fileLock = SimpleFileLock.Create(lockPath, TimeSpan.FromMinutes(3));

            if (fileLock.TryAcquireLock() || Debugger.IsAttached)
            {
                try
                {
                    return a();
                }
                finally
                {
                    fileLock.ReleaseLock();
                }
            }
            else
            {
                throw new StopDeploymentException($"Could not run operation {operation} because the application is locked in another process {lockPath}.");
            }
        }

        public Deployment SyncApp()
        {
            this.DeploymentActive.GrabDeployers(this.Logger).SyncAll();
            this.DeploymentActive.GrabServices(this.Logger).SyncAll();
            return this.DeploymentActive;
        }

        /// <summary>
        /// Deploys an installed app.
        /// </summary>
        public void RestartApp()
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot restart an undeployed application.");
            }

            this.Logger.LogInfo(false, "Restarting application: '{0}'", this.installedAppSettings.GetId());

            var deployersActive = this.DeploymentActive.GrabDeployers(this.Logger);
            var servicesActive = this.DeploymentActive.GrabServices(this.Logger);

            // Stop all
            deployersActive.StopAll();
            servicesActive.StopAll();

            // Start all
            servicesActive.StartAll();
            deployersActive.StartAll();
        }

        public void StartApp()
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot start an undeployed application.");
            }

            this.Logger.LogInfo(false, "Starting application: '{0}'", this.installedAppSettings.GetId());

            var deployersActive = this.DeploymentActive.GrabDeployers(this.Logger);
            var servicesActive = this.DeploymentActive.GrabServices(this.Logger);

            // Start all
            servicesActive.StartAll(true);
            deployersActive.StartAll(true);
        }

        public void StopApp()
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot stop an undeployed application.");
            }

            this.Logger.LogInfo(false, "Stopping application: '{0}'", this.installedAppSettings.GetId());

            var deployersActive = this.DeploymentActive.GrabDeployers(this.Logger);
            var servicesActive = this.DeploymentActive.GrabServices(this.Logger);

            // Start all
            servicesActive.StopAll(true);
            deployersActive.StopAll(true);
        }

        /// <summary>
        /// Application cleanup
        /// </summary>
        public void CleanupApp()
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot cleanup an undeployed application.");
            }

            this.Logger.LogInfo(false, "Cleanning up application: '{0}'", this.installedAppSettings.GetId());

            var deployersActive = this.DeploymentActive.GrabDeployers(this.Logger);
            var servicesActive = this.DeploymentActive.GrabServices(this.Logger);

            deployersActive.CleanupAll();
            servicesActive.CleanupAll();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RunCron()
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot cron an undeployed application.");
            }

            this.RunActionIfApplicationNotLocked(this.DoRunCron, $"Running cron for application {this.installedAppSettings.GetId()}");
        }

        /// <summary>
        /// Run ssl deployment of IisDeployer
        /// </summary>
        public void DeploySsl(bool forceRenewal)
        {
            if (this.DeploymentActive == null)
            {
                throw new Exception("Cannot DeploySsl on an undeployed application.");
            }

            this.RunActionIfApplicationNotLocked(() => this.DoDeploySsl(forceRenewal), $"Running DeploySsl for application {this.installedAppSettings.GetId()}");
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DoDeploySsl(bool forceRenewal)
        {
            var iisDeployer = (IISDeployer)this.DeploymentActive.GrabDeployers(this.Logger).SingleOrDefault(i => i is IISDeployer);
            iisDeployer?.SslCertificateRenewalCheck(forceRenewal);

            // Because there might have been changes in the stored persitent data, update the storage
            this.DeploymentActive.StoreInPath(this.activeDeploymentPathStorage);

            return true;
        }

        protected bool DoRunCron()
        {
            this.Logger.LogInfo(true, "Running cron for application: '{0}'", this.installedAppSettings.GetId());

            var deployersActive = this.DeploymentActive.GrabDeployers(this.Logger);
            var servicesActive = this.DeploymentActive.GrabServices(this.Logger);

            deployersActive.Cron(true);
            servicesActive.Cron(true);

            // Because there might have been changes in the stored persitent data, update the storage
            this.DeploymentActive.StoreInPath(this.activeDeploymentPathStorage);

            return true;
        }

        /// <summary>
        /// Deploys an installed app.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="force"></param>
        /// <param name="buildId"></param>
        /// <param name="sync"></param>
        protected Deployment _DeployApp(
            Application app,
            bool force = false,
            string buildId = null,
            bool sync = false)
        {
            DateTime start = DateTime.Now;

            // The parent application to inherit from (if needed)
            InstalledApplication parentApp = null;

            // Lo primero es ver si hay algo nuevo...
            var downloader = this.installedAppSettings.GetDownloader(this.GlobalSettings, this.Logger);

            if (!string.IsNullOrWhiteSpace(buildId))
            {
                this.Logger.LogInfo(true, "Deploying specific version build: '{0}'", buildId);
            }

            string nextArtifactId;

            // Next artifact id might be pulled from a remote location, and this prompt to random failures (network, etc.)
            // so wrap this in a try/catch
            try
            {
                nextArtifactId = downloader.GetNextId(buildId == "latest" ? null : buildId);
            }
            catch (Exception e)
            {
                this.Logger.LogException(new Exception("Failure while looking for next build ID", e), EventLogEntryType.Warning);
                return this.DeploymentActive;
            }

            string currentArtifactId = this.DeploymentActive != null ? this.DeploymentActive.artifact.id : string.Empty;

            bool isNew = this.DeploymentActive == null || this.DeploymentActive.artifact.id != nextArtifactId;

            // Check that Inherit application exists
            if (!string.IsNullOrEmpty(this.installedAppSettings.GetInherit()))
            {
                parentApp = app.GetInstalledApp(this.installedAppSettings.GetInherit());
                if (parentApp == null)
                {
                    throw new Exception(
                        $"Application from inheritation: {this.installedAppSettings.GetInherit()}, can not be found");
                }

                this.Logger.LogInfo(true, "Application configured to inherit from parent application '{0}'. Sync:{1}", parentApp.GetId(), sync ? "Yes" : "No");
            }

            // Si no es nuevo y no estamos forzando, no hacer deploy.
            if (!isNew && !force)
            {
                this.Logger.LogInfo(true, "No new version found for Application {0}", this.installedAppSettings.GetId());
                return this.DeploymentActive;
            }

            // There is an existing deployment that had a manually enforced BuildId
            if (!string.IsNullOrEmpty(this.DeploymentActive?.enforceBuildId))
            {
                if (force)
                {
                    if (!string.IsNullOrWhiteSpace(buildId))
                    {
                        // If there is a force and a buildId has been specified, override the next artifactId
                        // with the requested BuildId, or if latest was specified use that.
                        if (buildId != "latest")
                        {
                            nextArtifactId = buildId;
                        }
                    }
                    else
                    {
                        // If no specific build was requested, override the nextArtifactId with
                        // the stored build
                        nextArtifactId = this.DeploymentActive.enforceBuildId;
                        this.Logger.LogWarning(true, "Deploying stored version {0}", nextArtifactId);
                    }
                }
                else if (buildId != this.DeploymentActive.enforceBuildId
                    && buildId != "latest")
                {
                    this.Logger.LogWarning(true, $"Deployment was skipped because previous deployment was a version-specific deployment. Previous buildId='{this.DeploymentActive.enforceBuildId}'. Requested buildId='{buildId}'. Use buildId='latest' to force deploying the latest succesful build or -Force to deploy this version.");
                    return this.DeploymentActive;
                }
            }

            this.Logger.LogInfo(false, "@@ Starting deployment for app: '{0}'", this.installedAppSettings.GetId());
            this.Logger.LogInfo(false, "Current artifact: '{0}' || Previous artifact: '{1}'", nextArtifactId, currentArtifactId);

            // Specify a local temporary artifact location, in case this is supported by the downloader...
            // final path should be retrieved from artifact.localPath
            string preferredLocalArtifactPath =
                UtilsSystem.EnsureDirectoryExists(
                UtilsSystem.CombinePaths(
                    this.GlobalSettings.GetDefaultApplicationStorage().path,
                    "_tmp",
                    this.installedAppSettings.GetId(),
                    UtilsEncryption.GetShortHash(nextArtifactId, 12)),
                true);

            // Get from the ID... 
            Artifact artifact = downloader.PullFromId(nextArtifactId, preferredLocalArtifactPath);

            if (string.IsNullOrWhiteSpace(this.GlobalSettings.id))
            {
                throw new Exception("Environment settings cannot have an empty ID.");
            }

            this.Logger.LogInfo(false, "Environment id: '{0}'", this.GlobalSettings.id);
            this.Logger.LogInfo(false, "Environment options/tags: '{0}'", string.Join(",", this.GlobalSettings.getOptions()));
            this.Logger.LogInfo(false, "Pull artifact lapsed: {0}s", (DateTime.Now - start).TotalSeconds);

            start = DateTime.Now;

            // Look for a configuration file that fits this environment.
            string chefsettingsdir = UtilsSystem.CombinePaths(artifact.localPath, "chef");

            // The final chef configuration files is a combination of Chef files
            var appSettings = this.LoadApplicationSettings(
                chefsettingsdir,
                this.GlobalSettings.id,
                artifact.artifactSettings.branch,
                out var loadedConfigurationFiles);

            // Storage for current deployment. Includes all possible environment data
            // in order to provide traceability + rollback capabilities.
            Deployment deployment = new Deployment(
                appSettings,
                this.GlobalSettings,
                artifact,
                this.installedAppSettings,
                parentApp);

            deployment.SetPreviousDeployment(this.DeploymentActive);

            // Check the deployment windows!
            var deploymentSettings = deployment.appSettings.getDeploymentSettings();

            if (deploymentSettings != null)
            {
                if (deploymentSettings.deployment_windows != null
                    && deploymentSettings.deployment_windows.Any())
                {
                    bool canDeploy = false;

                    foreach (var deploymentWindow in deploymentSettings.deployment_windows)
                    {
                        TimeZoneInfo info = TimeZoneInfo.FindSystemTimeZoneById(deploymentWindow.Value.timezone);

                        TimeSpan dtStart = TimeSpan.Parse(deploymentWindow.Value.start);
                        TimeSpan dtEnd = TimeSpan.Parse(deploymentWindow.Value.end);

                        DateTimeOffset localServerTime = DateTimeOffset.Now;
                        DateTimeOffset windowTimeZone = TimeZoneInfo.ConvertTime(localServerTime, info);
                        TimeSpan dtNow = windowTimeZone.TimeOfDay;

                        if (dtStart <= dtEnd)
                        {
                            // start and stop times are in the same day
                            if (dtNow >= dtStart && dtNow <= dtEnd)
                            {
                                // current time is between start and stop
                                canDeploy = true;
                                break;
                            }
                        }
                        else
                        {
                            // start and stop times are in different days
                            if (dtNow >= dtStart || dtNow <= dtEnd)
                            {
                                // current time is between start and stop
                                canDeploy = true;
                                break;
                            }
                        }
                    }

                    // Even if we are not in a deployment windows,
                    // if we are forcing the deployment continue.
                    if (!canDeploy && !force)
                    {
                        this.Logger.LogInfo(false, "Application deployment skipped. Current time not within allowed publishing windows.");
                        return this.DeploymentActive;
                    }
                }
            }

            // Inform about the confiugration files that where used for loading
            deployment.SetRuntimeSetting("deployment.loaded_configuration_files", string.Join(",", loadedConfigurationFiles));

            deployment.enforceBuildId = buildId == "latest" ? null : buildId;

            var deployersActive = this.DeploymentActive != null ? this.DeploymentActive.GrabDeployers(this.Logger) : new DeployerCollection(this.GlobalSettings, null, this.Logger, parentApp);
            var servicesActive = this.DeploymentActive != null ? this.DeploymentActive.GrabServices(this.Logger) : new DeployerCollection(this.GlobalSettings, null, this.Logger, parentApp);

            var deployers = deployment.GrabDeployers(this.Logger);
            var services = deployment.GrabServices(this.Logger);

            this.Logger.LogInfo(false, "Deployers and services gathered. Starting installation...");

            var settingsConverter = new JObjectToKeyValueConverter();

            try
            {
                // Deploy the application base storage (logs, runtime, etc.)
                deployers.DeployAll();
                services.DeployAll();

                // Move the application settings to runtime settings
                var userApplicationSettings = appSettings.getApplicationSettings();
                foreach (var k in settingsConverter.NestedToKeyValue(userApplicationSettings))
                {
                    deployment.SetRuntimeSetting("app_settings." + k.Key, k.Value);
                }

                // Sync
                if (sync)
                {
                    deployers.SyncAll();
                    services.SyncAll();
                }

                // Time to hot switch the sites... we need to waitPauseMs for all
                // current requests to finish... because that way we ensure
                // that underlying storage updates will not collide if updates
                // are being deployed.
                servicesActive.StopAll();
                deployersActive.StopAll();

                // Some stuff requires the old services to be stopped in order to be deployed, such as IIS bindings and certificates
                deployers.BeforeDoneAll();
                services.BeforeDoneAll();

                var settingsToDeploy = deployment.GetRuntimeSettingsToDeploy();

                // Store Key-Value settings in a JSON object (with keys as 
                var jsonSettings = JsonConvert.SerializeObject(
                    settingsToDeploy,
                    Formatting.Indented);

                var jsonSettingsNested = JsonConvert.SerializeObject(
                    settingsConverter.keyValueToNested(settingsToDeploy),
                    Formatting.Indented);

                // Make sure we persist the settings AFTER all deployers have finished thri job
                deployers.DeploySettingsAll(jsonSettings, jsonSettingsNested);
                services.DeploySettingsAll(jsonSettings, jsonSettingsNested);

                // Time to start!
                deployers.StartAll();
                services.StartAll();

                DateTime dtStart = DateTime.Now;

                // Replace active configuration settings
                deployment.StoreInPath(this.activeDeploymentPathStorage);

                // Quitar el deployment anterior y si hay error seguir,
                // ya que los datos del deployment actual YA están guardados!
                servicesActive.UndeployAll(true);
                deployersActive.UndeployAll(true);

                // The done "event" is called on deployers
                // once everything is completed correctly.
                deployers.DoneAll(true);
                services.DoneAll(true);

                // Make sure that at least 2 seconds pass after deployment before
                // doing an OK to let IIS reconfigure.
                while ((DateTime.Now - dtStart).TotalSeconds < 1)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
            catch (Exception e)
            {
                // Just in case.... log this ASAP
                this.Logger.LogException(
                    new Exception("Error deploying APP: " + deployment.installedApplicationSettings.GetId(), e));

                deployers.StopAll(true);
                deployers.UndeployAll(true);

                // Aquí hacemos un continue on error porque... estamos repescando algo que ya funcionaba
                // a toda costa queremos levantarlo!
                deployersActive.StartAll(true);
                servicesActive.StopAll(true);

                // In unit test rethrow to preserve stack trace in GUI
                if (UnitTestDetector.IsRunningInTests)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e)
                        .Throw();
                }
                else
                {
                    throw new AlreadyHandledException(e.Message, e);
                }
            }
            finally
            {
                // Run cleanup, dot not fail if cleanup fails, it's just an extra...
                deployers.CleanupAll(true);
                services.CleanupAll(true);
            }

            // Done!
            this.Logger.LogInfo(false, "Deployment lapsed: {0}s", (DateTime.Now - start).TotalSeconds);

            return deployment;
        }
    }
}
