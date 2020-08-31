using iischef.utils;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace iischef.core.Configuration
{
    public class YamlConfigurationFile
    {
        /// <summary>
        /// Configuración general del deployment
        /// </summary>
        public JObject configuration { get; set; }

        protected string sourcePath { get; set; }

        public string getSourcePath()
        {
            return this.sourcePath;
        }

        public void ParseFromFile(string path)
        {
            try
            {
                this.sourcePath = path;
                this.ParseFromString(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                throw new Exception($"Error parsing YAML file [{path}] {e.Message} ", e);
            }
        }

        /// <summary>
        /// Parse the source config.
        /// </summary>
        /// <param name="source"></param>
        public void ParseFromString(string source)
        {
            try
            {
                this.configuration = UtilsYaml.YamlOrJsonToKtoken<JObject>(source);
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing YAML " + e.Message, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetStringValue(string key, string defaultValue)
        {
            if (this.configuration[key] != null)
            {
                return this.configuration[key].ToString();
            }

            return defaultValue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int GetIntValue(string key, int defaultValue = 0)
        {
            if (this.configuration[key] != null)
            {
                return Convert.ToInt32(this.configuration[key]);
            }

            return defaultValue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        public void Merge(YamlConfigurationFile source)
        {
            // Use default concat for arrays
            var mergeSettings = new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Concat
            };

            this.configuration.Merge(source.configuration, mergeSettings);
        }
    }
}
