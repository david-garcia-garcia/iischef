using iischef.core.Configuration;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Exception = System.Exception;

namespace iischef.core
{
    /// <summary>
    /// Represents a deployment. Used to unmount previous applications (and to know what is currently deployed here...)
    /// </summary>
    public class Deployment
    {
        /// <summary>
        /// Load a deployment from a serialized file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="globalSettings">The global settings (might have changed sin object was serialized)</param>
        /// <returns></returns>
        public static Deployment InstanceFromPath(string filePath, EnvironmentSettings globalSettings)
        {
            if (globalSettings == null)
            {
                throw new Exception("To deserialize a deployment you must provide updated global settings.");
            }

            if (!File.Exists(filePath))
            {
                throw new Exception("Could not load Deployment instance from path: " + filePath);
            }

            var contents = File.ReadAllText(filePath);

            Deployment result = null;

            try
            {
                result = JsonConvert.DeserializeObject<Deployment>(
                    contents,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });
            }
            catch (Exception e)
            {
                throw new Exception("Unable to deserialize Deployment json state from path: " + filePath, e);
            }

            if (result == null)
            {
                throw new Exception("Unable to deserialize Deployment json state from path: " + filePath);
            }

            result.loadedFromPath = filePath;
            result.globalSettings = globalSettings;

            return result;
        }

        public Deployment()
        {
        }

        protected Deployment previousDeployment;

        /// <summary>
        /// Get the previous deployment instance, if any.
        /// </summary>
        /// <returns></returns>
        public Deployment GetPreviousDeployment()
        {
            return this.previousDeployment;
        }

        /// <summary>
        /// Set the previous deployment
        /// </summary>
        /// <param name="previous"></param>
        public void SetPreviousDeployment(Deployment previous)
        {
            if (previous == null)
            {
                return;
            }

            this.previousDeployment = previous;
            this.previousDeployment.previousDeployment = null;
            this.privateDataPersistent = previous?.privateDataPersistent;
        }

        /// <summary>
        /// Expand runtime paths
        /// </summary>
        /// <returns></returns>
        public string ExpandPaths(string value)
        {
            value = value.Replace("%APP%", this.appPath);
            value = value.Replace("%RUNTIME%", this.runtimePath);
            value = value.Replace("%RUNTIME_WRITABLE%", this.runtimePathWritable);
            value = value.Replace("%LOG%", this.logPath);
            value = value.Replace("%TEMP%", this.tempPath);
            value = value.Replace("%DEPLOYMENTID%", this.shortid);
            return value;
        }

        public Deployment(
            ApplicationSettings appSettings,
            EnvironmentSettings globalSettings,
            Artifact source,
            InstalledApplication installedApplicationSettings,
            InstalledApplication parentInstalledApplicationSettings)
        {
            this.artifact = source;
            this.id = (Guid.NewGuid()).ToString();
            this.appSettings = appSettings;
            this.globalSettings = globalSettings;
            this.installedApplicationSettings = installedApplicationSettings;

            // We have a prefix to allow services to easily identify
            // chef bound resources.
            this.shortid = this.GetShortIdPrefix() + this.ShortHash(this.id, 4).ToLower();
            if (!this.IsShortId(this.shortid))
            {
                throw new Exception("Invalid shortId generated ????");
            }

            this.runtimeSettings = new Dictionary<string, string>();
            this.parentInstalledApplicationSettings = parentInstalledApplicationSettings;
            this.DeploymentUnixTime = (long)DateTime.UtcNow.ToUnixTimestamp();
            this.DeployRuntimeSettings();
        }

        /// <summary>
        /// Get the prefix used for this deployment shortid.
        /// </summary>
        /// <returns></returns>
        public string GetShortIdPrefix()
        {
            return "chf_" + this.installedApplicationSettings.GetId() + "_";
        }

        public bool IsShortId(string id)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                id,
                $"^{System.Text.RegularExpressions.Regex.Escape(this.GetShortIdPrefix())}([A-Za-z]{{4}}$)");
        }

        public void DeployRuntimeSettings()
        {
            var deployment = this;

            deployment.SetRuntimeSetting("deployment.shortId", deployment.getShortId());
            deployment.SetRuntimeSetting("deployment.time", DateTime.UtcNow.ToString("dd/MM/yyyy HH-mm-ss.fff"));
            deployment.SetRuntimeSetting("deployment.environment.MachineName", Environment.MachineName);

            deployment.SetRuntimeSetting("deployment.artifact.branch", deployment.artifact.artifactSettings.branch);
            deployment.SetRuntimeSetting("deployment.artifact.version", deployment.artifact.artifactSettings.version);
            deployment.SetRuntimeSetting("deployment.artifact.id", deployment.artifact.id);
            deployment.SetRuntimeSetting("deployment.artifact.commit_sha", deployment.artifact.artifactSettings.commit_sha);
            deployment.SetRuntimeSetting("deployment.artifact.type", deployment.GetType().Name);

            deployment.SetRuntimeSetting("installedApp.id", deployment.installedApplicationSettings.GetId());
        }

        /// <summary>
        /// Grab the application limits to apply for this deployment
        /// </summary>
        /// <returns></returns>
        public ApplicationLimits GetApplicationLimits()
        {
            var limits = this.installedApplicationSettings?.GetApplicationLimits();

            // If there are no application specific settings, use the system wide settings
            if (limits == null)
            {
                // Grab them from the global settings (if any...)
                limits = this.globalSettings.defaultApplicationLimits;

                if (limits == null)
                {
                    limits = new ApplicationLimits();
                }
            }

            ApplicationLimits.PopulateDefaultsIfMissing(limits);

            return limits;
        }

        /// <summary>
        /// Grab the deployers for an application
        /// </summary>
        /// <returns></returns>
        public DeployerCollection GrabDeployers(ILoggerInterface logger)
        {
            Dictionary<string, Type> deployerTypes = new Dictionary<string, Type>()
            {
                { "php", typeof(Php.PhpDeployer) },
                { "iis", typeof(IIS.IISDeployer) },
                { "app", typeof(Storage.AppBaseStorageDeployer) },
            };

            var deployers = new DeployerCollection(this.globalSettings, this, logger, this.parentInstalledApplicationSettings);

            foreach (var d in this.appSettings.getDeployers())
            {
                var type = (string)d.Value["type"];

                if (type == null || !deployerTypes.ContainsKey(type))
                {
                    throw new Exception($"Deployer type '{type}' not found.");
                }

                Type deployertype = deployerTypes[type];

                deployers.AddItem(deployertype, (JObject)d.Value);
            }

            return deployers;
        }

        public DeployerCollection GrabServices(ILoggerInterface logger)
        {
            Dictionary<string, Type> serviceTypes = new Dictionary<string, Type>()
            {
                { "sqlsrv", typeof(Services.SQLService) },
                { "disk", typeof(Services.DiskService) },
                { "couchbase", typeof(Services.CouchbaseService) },
                { "scheduler", typeof(Services.ScheduleService) }
            };

            var services = new DeployerCollection(this.globalSettings, this, logger, this.parentInstalledApplicationSettings);

            foreach (var d in this.appSettings.getServices())
            {
                var type = (string)d.Value["type"];

                if (!serviceTypes.ContainsKey(type))
                {
                    throw new Exception("Service type not found:" + type);
                }

                Type serviceType = serviceTypes[type];
                services.AddItem(serviceType, (JObject)d.Value);
            }

            return services;
        }

        /// <summary>
        /// Store a serialized version of this in a path
        /// </summary>
        /// <param name="path"></param>
        public void StoreInPath(string path)
        {
            var temporaryPath = path + Guid.NewGuid() + ".tmp";
            var backupPath = path + Guid.NewGuid() + ".bak";

            UtilsSystem.EnsureDirectoryExists(path);

            var serialized = JsonConvert.SerializeObject(
                this,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });

            File.WriteAllText(temporaryPath, serialized);

            try
            {
                Deployment.InstanceFromPath(temporaryPath, this.globalSettings);
            }
            catch (Exception e)
            {
                throw new Exception("Error while storing configuration file, corrupted file path: " + temporaryPath, e);
            }

            // Make this more robust, to avoid corrupted active configurations, we ensure that the active
            // configuration can be read before setting it as active. File.Move() is less error to corruption
            // than the actual writing to disk operation
            if (File.Exists(path))
            {
                File.Move(path, backupPath);
            }

            File.Move(temporaryPath, path);

            File.Delete(backupPath);
        }

        #region Default directories

        // All applications must have at least
        // 4 paths:
        // appPath: Application path. This is where the artifact is deployed. I.e. c:\_webs\mydeploymentslot\app
        // runtimePath: Runtime path. Any runtime specifics (such as PHP runtime, libraries, etc). I.e. c:\_webs\mydeploymentslot\runtime
        // logPath: Path for log files. Usually in a disk that can be filled without affecting application. I.e. e:\volatile\appid\logs
        // tempPath: Path for temp files. Usually in a disk that can be filled without affecting application. I.e. e:\volatile\appid\temp
        // Log and temp paths are preserved between builds, but are not guaranteed to be persistent.

        /// <summary>
        /// If this was loaded from a path, the path.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "Important Region")]
        public string loadedFromPath { get; set; }

        /// <summary>
        /// Application path
        /// </summary>
        public string appPath { get; set; }

        /// <summary>
        /// This is the userPrincipalName and matches the account name!
        /// </summary>
        public string windowsUsername { get; set; }

        /// <summary>
        /// Get the fully qualified domain name of the current username
        /// </summary>
        /// <param name="preWindows2000">If true, it will return DOMAIN\SamAccountName</param>
        /// <returns>MACHINE\samAccountName in local accounts, userPrincipalName@domain in AD</returns>
        public string WindowsUsernameFqdn(bool preWindows2000 = false)
        {
            return this.globalSettings.FormatUserNameForPrincipal(this.windowsUsername, preWindows2000);
        }

        /// <summary>
        /// The windows principal user name, this is used even for local accounts, as there is a direct map to a SamAccountName
        /// </summary>
        /// <returns></returns>
        public string WindowsUserPrincipalName()
        {
            return this.windowsUsername;
        }

        /// <summary>
        /// Get the password. This is generated realtime as a hash of the
        /// username itself using the installation salt.
        /// </summary>
        /// <returns></returns>
        public string GetWindowsPassword()
        {
            // On some setups there is policy enforcement...
            // https://technet.microsoft.com/en-us/library/hh994562(v=ws.11).aspx
            var pwd = "#" + UtilsEncryption.GetMD5(this.windowsUsername + this.globalSettings.installationSalt).Substring(0, 10)
                + UtilsEncryption.GetMD5(this.windowsUsername + this.globalSettings.installationSalt).Substring(10, 10).ToUpper();

            return pwd;
        }

        /// <summary>
        /// Path for the runtime, with limited set of permissions (read/execute)
        /// </summary>
        public string runtimePath { get; set; }

        /// <summary>
        /// Writable runtime path
        /// </summary>
        public string runtimePathWritable { get; set; }

        /// <summary>
        /// Path for the logs
        /// </summary>
        public string logPath { get; set; }

        /// <summary>
        /// Path for temp files
        /// </summary>
        public string tempPath { get; set; }

        /// <summary>
        /// Temporary path in local computer
        /// </summary>
        public string tempPathSys { get; set; }

        #endregion

        /// <summary>
        /// A copy of the configuration used for the deployment...
        /// </summary>
        public ApplicationSettings appSettings { get; set; }

        /// <summary>
        /// Global settings, for the record.
        /// </summary>
        public EnvironmentSettings globalSettings { get; set; }

        /// <summary>
        /// Time the deploy job started
        /// </summary>
        public DateTime? jobStart { get; set; }

        /// <summary>
        /// Time the deploy job ended
        /// </summary>
        public DateTime? jobEnd { get; set; }

        /// <summary>
        /// Storage for random data from modules/components.
        /// </summary>
        public Dictionary<string, object> privateData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Storage for random data from modules/components that is preserved between deployments
        /// </summary>
        public Dictionary<string, object> privateDataPersistent { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The deployment id, unique for each deployment.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// short id
        /// </summary>
        public string shortid { get; set; }

        /// <summary>
        /// The installed application settings
        /// </summary>
        public InstalledApplication installedApplicationSettings { get; set; }

        /// <summary>
        /// The exact unis time this deployment was made at
        /// </summary>
        public long? DeploymentUnixTime { get; set; }

        /// <summary>
        /// The exact artifact this deplyoyment came from.
        /// </summary>
        public Artifact artifact { get; set; }

        /// <summary>
        /// When the user has enforced deployment of a specific version
        /// through the UI, automatic updates should not be pushed.
        /// </summary>
        public string enforceBuildId { get; set; }

        /// <summary>
        /// Settings passed on to the application at runtime
        /// with information about directories, databases, etc..
        /// </summary>
        public Dictionary<string, string> runtimeSettings { get; set; }

        /// <summary>
        /// Inhert application.
        /// 
        /// </summary>
        public InstalledApplication parentInstalledApplicationSettings { get; set; }

        /// <summary>
        /// We can have runtime overrides at the deployedApplicationLevel
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetRuntimeSettingsToDeploy()
        {
            // The priority is:
            //
            // 0. Settings defined by the application itself
            // 1. Per installed application settings
            // 2. Server level settings
            // 3. Settings generated during install by services

            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var kvp in this.appSettings.getRuntimeSettingsOverrides())
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result.Add(kvp.Key, kvp.Value);
                }
            }

            foreach (var kvp in this.installedApplicationSettings.GetRuntimeSettingsOverrides())
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result.Add(kvp.Key, kvp.Value);
                }
            }

            foreach (var kvp in this.globalSettings.GetRuntimeOverrides())
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result.Add(kvp.Key, kvp.Value);
                }
            }

            foreach (var kvp in this.runtimeSettings)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result.Add(kvp.Key, kvp.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Components should use this to register runtime related
        /// settings for the application to consume.
        /// </summary>
        public void SetRuntimeSetting(string name, string value)
        {
            this.runtimeSettings[name] = value;
        }

        /// <summary>
        /// Get a value for a runtime setting
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetRuntimeSetting(string name, string defaultValue)
        {
            if (!this.runtimeSettings.ContainsKey(name))
            {
                return defaultValue;
            }

            return (string)this.runtimeSettings[name];
        }

        /// <summary>
        /// Used for stuff that needs to be maintained between deployments
        /// to allow for rollbacks, such as database name and credentials.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetOrSetRuntimeSettingPersistent(string name, string defaultValue)
        {
            string result = this.GetRuntimeSetting(name, defaultValue);

            if (this.previousDeployment != null)
            {
                result = this.previousDeployment.GetRuntimeSetting(name, defaultValue);
            }

            // Look for any overrides....
            if (this.installedApplicationSettings.GetRuntimeSettingsOverrides().ContainsKey(name))
            {
                result = this.installedApplicationSettings.GetRuntimeSettingsOverrides()[name];
            }

            this.SetRuntimeSetting(name, result);

            return result;
        }

        public string getShortId()
        {
            return this.shortid;
        }

        /// <summary>
        /// Get a replacer to deploy runtime settings in templates.
        /// </summary>
        /// <returns></returns>
        public RuntimeSettingsReplacer GetSettingsReplacer()
        {
            return new RuntimeSettingsReplacer(this.GetRuntimeSettingsToDeploy());
        }

        /// <summary>
        /// Get a setting
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public Dictionary<string, TType> GetSettingCollection<TType>(string collection)
        {
            if (this.privateData == null || !this.privateData.ContainsKey(collection))
            {
                this.privateData[collection] = new Dictionary<string, TType>();
            }

            if (this.privateData[collection] is JObject)
            {
                this.privateData[collection] = (this.privateData[collection] as JObject).castTo<Dictionary<string, TType>>();
            }

            return (Dictionary<string, TType>)this.privateData[collection];
        }

        /// <summary>
        /// Set a setting.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetSettingCollection<TType>(string collection, string name, TType value)
        {
            this.privateData = this.privateData ?? new Dictionary<string, object>();

            if (!this.privateData.ContainsKey(collection))
            {
                this.privateData[collection] = new Dictionary<string, TType>();
            }

            ((Dictionary<string, TType>)this.privateData[collection])[name] = value;
        }

        /// <summary>
        /// Get a setting
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <param name="logger"></param>
        /// <param name="isEnum"></param>
        /// <returns></returns>
        public TType GetSetting<TType>(string name, TType defaultValue, ILoggerInterface logger, bool isEnum = false)
        {
            this.privateData = this.privateData ?? new Dictionary<string, object>();

            TType result = defaultValue;

            if (!this.privateData.ContainsKey(name))
            {
                return defaultValue;
            }

            try
            {
                if (isEnum)
                {
                    result = (TType)Enum.Parse(typeof(TType), Convert.ToString(this.privateData[name]));
                }
                else
                {
                    result = (TType)this.privateData[name];
                }
            }
            catch (Exception e)
            {
                logger.LogInfo(false, "source value: '" + Convert.ToString(this.privateData[name]) + "'");
                logger.LogException(e);
            }

            return result;
        }

        /// <summary>
        /// Set a setting.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetSetting(string name, object value)
        {
            this.privateData[name] = value;
        }

        /// <summary>
        /// Get a setting
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <param name="logger"></param>
        /// <param name="isEnum"></param>
        /// <returns></returns>
        public TType GetSettingPersistent<TType>(string name, TType defaultValue, ILoggerInterface logger, bool isEnum = false)
        {
            this.privateDataPersistent = this.privateDataPersistent ?? new Dictionary<string, object>();

            TType result = defaultValue;

            if (!this.privateDataPersistent.ContainsKey(name))
            {
                return defaultValue;
            }

            try
            {
                if (isEnum)
                {
                    result = (TType)Enum.Parse(typeof(TType), Convert.ToString(this.privateDataPersistent[name]));
                }
                else
                {
                    result = (TType)this.privateDataPersistent[name];
                }
            }
            catch (Exception e)
            {
                logger.LogInfo(false, "source value: '" + Convert.ToString(this.privateDataPersistent[name]) + "'");
                logger.LogException(e);
            }

            return result;
        }

        /// <summary>
        /// Set a setting.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetSettingPersistent(string name, object value)
        {
            this.privateDataPersistent = this.privateDataPersistent ?? new Dictionary<string, object>();
            this.privateDataPersistent[name] = value;
        }

        /// <summary>
        /// TODO: Move this to utils library.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected string ShortHash(string input, int length = 3)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));

                // make sure the hash is only alpha numeric to prevent charecters that may break the url
                return string.Concat(Convert.ToBase64String(hash).ToCharArray().Where(x => char.IsLetter(x)).Take(length));
            }
        }
    }
}
