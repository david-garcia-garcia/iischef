using iischef.core.AppVeyorMonitor;
using iischef.core.Configuration;
using iischef.core.IIS;
using iischef.utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.core.SystemConfiguration
{
    /// <summary>
    /// TODO: THIS SHOULD ALSO BE SPLIT INTO COMPONENTS
    /// THAT ARE PLUGGABLE AND VALIDATE THEMSELVES...
    /// </summary>
    public class EnvironmentSettings
    {
        /// <summary>
        /// Get default application storage.
        /// </summary>
        /// <returns></returns>
        public StorageLocation GetDefaultApplicationStorage()
        {
            if (this.applicationStorages == null || !this.applicationStorages.Any())
            {
                throw new Exception("No application storages defined.");
            }

            var result = (from p in this.applicationStorages
                          where p.id == this.primaryApplicationStorage
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception("Default app storage not found: " + this.primaryContentStorage);
            }

            return result;
        }

        /// <summary>
        /// Get default storage for log files
        /// </summary>
        /// <returns></returns>
        public StorageLocation GetDefaultLogStorage()
        {
            if (this.logStorages == null || !this.logStorages.Any())
            {
                throw new Exception("No log storages defined.");
            }

            var result = (from p in this.logStorages
                          where p.id == this.primaryLogStorage
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception("Default log storage not found: " + this.primaryContentStorage);
            }

            return result;
        }

        /// <summary>
        /// Get default storage for temporary files
        /// </summary>
        /// <returns></returns>
        public StorageLocation GetDefaultTempStorage()
        {
            if (this.tempStorages == null || !this.tempStorages.Any())
            {
                throw new Exception("No temp storages defined.");
            }

            var result = (from p in this.tempStorages
                          where p.id == this.primaryTempStorage
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception("Default temp storage not found: " + this.primaryContentStorage);
            }

            return result;
        }

        /// <summary>
        /// Get the default storage for content files
        /// </summary>
        /// <returns></returns>
        public StorageLocation GetDefaultContentStorage()
        {
            if (this.contentStorages == null || !this.contentStorages.Any())
            {
                throw new Exception("No content storages defined.");
            }

            var result = (from p in this.contentStorages
                          where p.id == this.primaryContentStorage
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception("Default content storage not found: " + this.primaryContentStorage);
            }

            return result;
        }

        /// <summary>
        /// Get information for a sql server. Throws an exception
        /// if the server cannot be located.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public SQLServer GetSqlServer(string target = null)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                target = this.primarySqlServer;
            }

            if (this.sqlServers == null || !this.sqlServers.Any())
            {
                throw new Exception("No sql servers defined.");
            }

            var result = (from p in this.sqlServers
                          where p.id == target
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception($"Default sql server not found: '{target}'");
            }

            return result;
        }

        public CouchbaseServer GetDefaultCouchbaseServer()
        {
            if (this.couchbaseServers == null || !this.couchbaseServers.Any())
            {
                throw new Exception("No couchbase servers defined in current server.");
            }

            var result = (from p in this.couchbaseServers
                          where p.id == this.primaryCouchbaseServer
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception("Default couchbase server not found: " + this.primaryCouchbaseServer);
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public NetworkInterface FindEndpointAddress(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new Exception("Endpoint id is empty.");
            }

            if (id == "local" || this.options.Contains("iis-force-all-local"))
            {
                return new NetworkInterface()
                {
                    ip = UtilsIis.LOCALHOST_ADDRESS,
                    forcehosts = true,
                    id = "local"
                };
            }

            if (this.endpoints == null || !this.endpoints.Any())
            {
                throw new Exception("The current environment does not have any configured endpoints. Only the 'local' endpoint is valid. Requested " + id);
            }

            var result = (from p in this.endpoints
                          where p.id == id
                          select p).SingleOrDefault();

            if (result == null)
            {
                throw new Exception(string.Format("Could not find endpoint with id '{0}'. Available: {1}", id, string.Join(",", this.endpoints.Select((i) => i.id + "(" + i.ip + ")"))));
            }

            return result;
        }

        /// <summary>
        /// Options/tags for the environment.
        /// </summary>
        /// <returns></returns>
        public List<string> getOptions()
        {
            if (this.options != null)
            {
                return this.options
                    .Select((i) => i.Trim().ToLower())
                    .Where((i) => !string.IsNullOrWhiteSpace(i))
                    .ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Options/tags for the environment
        /// </summary>
        public List<string> options { get; set; }

        /// <summary>
        /// Development artifacts NEED not to have
        /// a branch binding.
        /// </summary>
        public bool allowDevArtifacts { get; set; }

        /// <summary>
        /// Server name. Only applications targeted to this environment
        /// will be deployed here.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Directory to store settings for the application
        /// </summary>
        public string settingsDir { get; set; }

        /// <summary>
        /// Application templates directory
        /// </summary>
        public string applicationTemplateDir { get; set; }

        /// <summary>
        /// Active deployment directory
        /// </summary>
        public string activeDeploymentDir { get; set; }

        /// <summary>
        /// The acme provider to use
        /// </summary>
        public string AcmeProvider { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string AcmeUri { get; set; }

        /// <summary>
        /// Interval (s) at which deployments are checked.
        /// </summary>
        public int deployInterval { get; set; }

        /// <summary>
        /// Primar location for application storage
        /// </summary>
        public string primaryApplicationStorage { get; set; }

        /// <summary>
        /// Unique salt to generate passwords
        /// </summary>
        public string installationSalt { get; set; }

        /// <summary>
        /// Primary log files storage.
        /// </summary>
        public string primaryLogStorage { get; set; }

        /// <summary>
        /// Primary temporary files storage.
        /// </summary>
        public string primaryTempStorage { get; set; }

        /// <summary>
        /// Storage for temporary data
        /// </summary>
        public List<StorageLocation> tempStorages { get; set; }

        /// <summary>
        /// Storage for log files
        /// </summary>
        public List<StorageLocation> logStorages { get; set; }

        /// <summary>
        /// Locations to store application data
        /// </summary>
        public List<StorageLocation> applicationStorages { get; set; }

        /// <summary>
        /// Location where application storage will be set by default.
        /// </summary>
        public string primaryContentStorage { get; set; }

        /// <summary>
        /// Available storage locations.
        /// </summary>
        public List<StorageLocation> contentStorages { get; set; }

        /// <summary>
        /// Primary SQL Server for deployments
        /// </summary>
        public string primarySqlServer { get; set; }

        /// <summary>
        /// SQL Servers
        /// </summary>
        public List<SQLServer> sqlServers { get; set; }

        /// <summary>
        /// Primary couchbase server id
        /// </summary>
        public string primaryCouchbaseServer { get; set; }

        /// <summary>
        /// Bindings for the shared CDN
        /// </summary>
        public List<CdnBinding> cdn_bindings { get; set; }

        /// <summary>
        /// List of available couchbase servers
        /// </summary>
        public List<CouchbaseServer> couchbaseServers { get; set; }

        /// <summary>
        /// Available network endpoints
        /// </summary>
        public List<NetworkInterface> endpoints { get; set; }

        /// <summary>
        /// Privileged local accounts
        /// </summary>
        public List<LocalAccount> accounts { get; set; }

        /// <summary>
        /// Groups that chef auto generated users will be added to automatically
        /// </summary>
        public List<string> userGroups { get; set; }

        /// <summary>
        /// The directory principal
        /// </summary>
        public AccountManagementPrincipalContext directoryPrincipal { get; set; }

        /// <summary>
        /// Use this to monitor AppVeyor projects, and run
        /// automated deployments according to commit messages.
        /// </summary>
        public List<AppVeyorMonitorSettings> appVeyorMonitors { get; set; }

        /// <summary>
        /// Settings to add to ALL runtimes...
        /// </summary>
        public Dictionary<string, string> runtime_overrides { get; set; }

        /// <summary>
        /// List of safe IP addresses for the system
        /// </summary>
        public List<string> safeIpAddresses { get; set; }

        /// <summary>
        /// Global default application limits
        /// </summary>
        public ApplicationLimits defaultApplicationLimits { get; set; }

        /// <summary>
        /// Runtime overrides
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetRuntimeOverrides()
        {
            this.runtime_overrides = this.runtime_overrides ?? new Dictionary<string, string>();
            return this.runtime_overrides;
        }

        /// <summary>
        /// Format username for principal
        /// </summary>
        /// <returns></returns>
        public string FormatUserNameForPrincipal(string userPrincipalName, bool preWindows200)
        {
            if (this.directoryPrincipal != null)
            {
                return this.directoryPrincipal.FormatUserNameForPrincipal(userPrincipalName, preWindows200);
            }
            else
            {
                return Environment.MachineName + "\\" +
                       UtilsWindowsAccounts.SamAccountNameFromUserPrincipalName(userPrincipalName);
            }
        }
    }
}
