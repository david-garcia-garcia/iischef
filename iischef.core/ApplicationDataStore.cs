using iischef.utils;
using Newtonsoft.Json;
using System;
using System.IO;

namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class ApplicationDataStore
    {
        /// <summary>
        /// 
        /// </summary>
        private string OriginalHash = null;

        /// <summary>
        /// 
        /// </summary>
        public AppSettings AppSettings { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ApplicationDataStore()
        {
            if (!File.Exists(ApplicationDataStore.StorePath))
            {
                this.AppSettings = new AppSettings();
                return;
            }

            try
            {
                var contents = File.ReadAllText(ApplicationDataStore.StorePath);
                this.OriginalHash = UtilsEncryption.GetMD5(contents);
                this.AppSettings = JsonConvert.DeserializeObject<AppSettings>(contents);
            }
            catch (Exception ex)
            {
                throw new BusinessRuleException(
                    $"Unable to decode configuration settings at {ApplicationDataStore.StorePath}: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void Save()
        {
            var newContents = JsonConvert.SerializeObject(this.AppSettings, Formatting.Indented);

            if (UtilsEncryption.GetMD5(newContents) != this.OriginalHash)
            {
                File.WriteAllText(ApplicationDataStore.StorePath, newContents);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static string StorePath =>
            Path.Combine(
                Environment.ExpandEnvironmentVariables("%ProgramData%"),
                "iischefsettings.json");
    }
}
