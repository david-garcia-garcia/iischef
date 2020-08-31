using iischef.core.Downloaders;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.core.Configuration
{
    /// <summary>
    /// Configuration files used for file-based persistent deployment (AKA "From Template").
    /// </summary>
    public class InstalledApplication : YamlConfigurationFile
    {
        /// <summary>
        /// We can override runtime settings for an application
        /// (i.e. to make it point to a specific storage that does
        /// not match the default pattern assigned by chef).
        /// 
        /// Not all settings accept overriding, see the declaring component
        /// specification for support.
        /// </summary>
        public Dictionary<string, string> GetRuntimeSettingsOverrides()
        {
            if (this.configuration["runtime_overrides"] == null)
            {
                return new Dictionary<string, string>();
            }

            return this.configuration["runtime_overrides"].ToObject<Dictionary<string, string>>();
        }

        /// <summary>
        /// Get the application limits for this particular installed application
        /// </summary>
        /// <returns></returns>
        public ApplicationLimits GetApplicationLimits()
        {
            return this.configuration["application_limits"]?.ToObject<ApplicationLimits>();
        }

        /// <summary>
        /// The application uniqueId
        /// </summary>
        /// <returns></returns>
        public string GetId()
        {
            return this.configuration["id"].ToString();
        }

        /// <summary>
        /// How to mount the artifact once obtained from the 
        /// downloader:
        /// * move (fastest - good for production, destroys original source)
        /// * copy (the default)
        /// * symlink (use symlink)
        /// * junction (use junction)
        /// * origin (mount the application directly to the path provided by the downloader)
        /// </summary>
        /// <returns></returns>
        public string GetApplicationMountStrategy()
        {
            if (this.configuration["mount_strategy"] == null)
            {
                return "copy";
            }

            return this.configuration["mount_strategy"].ToString();
        }

        /// <summary>
        /// TTL for this application (will be removed). In HOURS.
        /// </summary>
        /// <returns></returns>
        public double GetExpires()
        {
            if (this.configuration["expires"] == null)
            {
                return 0;
            }

            return double.Parse((string)this.configuration["expires"]);
        }

        protected List<string> GetInternalTags()
        {
            if (!this.configuration["tags"].IsNullOrDefault())
            {
                var tags = System.Text.RegularExpressions.Regex.Split((string)this.configuration["tags"], ",");

                return (from t in tags
                        where !string.IsNullOrWhiteSpace(t)
                        select t.ToLower().Trim()).Distinct().ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Tags for this installation.
        /// </summary>
        /// <returns></returns>
        public List<string> GetTags()
        {
            List<string> applicationTags = new List<string>();
            applicationTags.AddRange(this.GetInternalTags());
            applicationTags.Add($"app-id-{this.GetId()}");
            return applicationTags;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringTags"></param>
        public void MergeTags(string stringTags)
        {
            var tags = System.Text.RegularExpressions.Regex.Split(stringTags, ",");
            var currentTags = this.GetInternalTags();
            currentTags.AddRange(tags);
            currentTags = currentTags.Select((i) => i.ToLower().Trim()).Distinct().ToList();
            this.configuration["tags"] = string.Join(", ", currentTags);
        }

        /// <summary>
        /// The downloader
        /// </summary>
        public IDownloaderInterface GetDownloader(EnvironmentSettings globalSettings, ILoggerInterface logger)
        {
            JObject downloader = (JObject)this.configuration["downloader"];

            string type = (string)downloader["type"];

            switch (type)
            {
                case "appveyor":
                    AppVeyorDownloaderSettings settings = downloader.castTo<AppVeyorDownloaderSettings>();
                    return new AppVeyorDownloader(settings, globalSettings, logger, globalSettings.GetDefaultTempStorage().path, this.GetId());
                case "localpath":
                    LocalPathDownloaderSettings settings2 = downloader.castTo<LocalPathDownloaderSettings>();
                    return new LocalPathDownloader(settings2, globalSettings, logger);
                case "localzip":
                    LocalZipDownloaderSettings settings3 = downloader.castTo<LocalZipDownloaderSettings>();
                    return new LocalZipDownloader(settings3, globalSettings, logger);
                default:
                    throw new Exception("Unkown downloader type:" + type);
            }
        }

        /// <summary>
        /// If this installed application should be automatically updated when new artifacts are available.
        /// </summary>
        /// <returns></returns>
        public bool GetAutodeploy()
        {
            if (bool.TryParse(this.configuration["autodeploy"]?.ToString(), out bool autodeploy))
            {
                return autodeploy;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public string GetInherit()
        {
            if (this.configuration["inherit"] == null)
            {
                return string.Empty;
            }

            return this.configuration["inherit"].ToString();
        }

        public InstalledApplication()
        {
        }
    }
}
