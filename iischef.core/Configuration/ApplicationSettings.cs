using iischef.utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.core.Configuration
{
    public class ApplicationSettings : YamlConfigurationFile
    {
        /// <summary>
        /// Metadatos del artefacto:
        /// --- # Artifact Settings 
        /// appveyor-project-id: 290514  
        /// appveyor-project-name: sabentisplus
        /// build-folder: C:\projects\sabentisplus
        /// build-id: 7357101 
        /// build-date: 03/07/2017 15:11:49
        /// repo-branch: 1.0.15 
        /// repo-name: sabentis/sabentisplus
        /// repo-is-tag: false 
        /// repo-commit: f5833842274eb67908574d105077c73d1d6479ce
        /// repo-commit-author: mr.grigorieva @gmail.com
        /// repo-commit-author-email: mr.grigorieva @gmail.com
        /// repo-commit-timestamp: 2017-03-07T09:39:10.0000000Z
        /// </summary>
        public JObject artifactMetadata { get; set; }

        /// <summary>
        /// Ubicación del artefacto en disco
        /// </summary>
        public string artifactPath { get; set; }

        public string getArtifactPath()
        {
            return this.artifactPath;
        }

        public void setArtifactPath(string path)
        {
            this.artifactPath = path;
        }

        /// <summary>
        /// Wether or not we should inherit from the default settings.
        /// </summary>
        /// <returns></returns>
        public string GetInherit()
        {
            return this.GetStringValue("inherit", null);
        }

        /// <summary>
        /// Regex that tells what branches to use this for.
        /// 
        /// Defaults to "All branches" (.*)
        /// </summary>
        /// <returns></returns>
        public string GetScopeBranchRegex()
        {
            return this.GetStringValue("scope-branch-regex", ".*");
        }

        /// <summary>
        /// Environment tags
        /// </summary>
        /// <returns></returns>
        public List<string> GetScopeTags()
        {
            return this.GetStringValue("scope-tags", string.Empty).Split(",".ToCharArray())
                .Where((i) => !string.IsNullOrWhiteSpace(i))
                .Select((i) => i.Trim().ToLower())
                .ToList();
        }

        /// <summary>
        /// Regex that tells what environments to use this for.
        /// 
        /// Defaults to "All environments" (.*)
        /// </summary>
        /// <returns></returns>
        public string getScopeEnvironmentRegex()
        {
            return this.GetStringValue("scope-environment-regex", ".*");
        }

        /// <summary>
        /// Get a scope weight.
        /// </summary>
        /// <returns></returns>
        public int getScopeWeight()
        {
            return this.GetIntValue("scope-weight", 0);
        }

        /// <summary>
        /// Saber si este paquete es dev (no tiene artifact metadata)
        /// </summary>
        /// <returns></returns>
        public bool isDev()
        {
            return this.artifactMetadata == null;
        }

        public Dictionary<string, JToken> getDeployers()
        {
            if (this.configuration["deployers"] == null)
            {
                return new Dictionary<string, JToken>();
            }

            // Para que haya BWC con implementación original basada en arrays
            return UtilsJson.keyedFromArrayOrObject(this.configuration["deployers"]);
        }

        /// <summary>
        /// Get the list of registered services
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, JToken> getServices()
        {
            if (this.configuration["services"] == null)
            {
                return new Dictionary<string, JToken>();
            }

            // Para que haya BWC con implementación original basada en arrays
            return UtilsJson.keyedFromArrayOrObject(this.configuration["services"]);
        }

        /// <summary>
        /// We can override runtime settings for an application
        /// (i.e. to make it point to a specific storage that does
        /// not match the default pattern assigned by chef).
        /// 
        /// Not all settings accept overriding, see the declaring component
        /// specification for support.
        /// </summary>
        public Dictionary<string, string> getRuntimeSettingsOverrides()
        {
            if (this.configuration["runtime_overrides"] == null)
            {
                return new Dictionary<string, string>();
            }

            return this.configuration["runtime_overrides"].ToObject<Dictionary<string, string>>();
        }

        /// <summary>
        /// Application settings are much like runtime overrides
        /// but in a nested form.
        /// </summary>
        /// <returns></returns>
        public JObject getApplicationSettings()
        {
            if (this.configuration["app_settings"] == null)
            {
                return new JObject();
            }

            return this.configuration["app_settings"] as JObject;
        }

        /// <summary>
        /// Get the deployment settings.
        /// </summary>
        /// <returns></returns>
        public DeploymentSettings getDeploymentSettings()
        {
            JToken config = this.configuration["deployment"];
            if (config == null)
            {
                return null;
            }

            return this.configuration["deployment"].ToObject<DeploymentSettings>();
        }
    }
}
