using iischef.utils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Cryptography;

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

                if (this.TryLoadPreviousSettings(out var previousPfxPassword))
                {
                    this.AppSettings.PfxPassword = previousPfxPassword;
                }

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

        private bool TryLoadPreviousSettings(out string previousPfxPassword)
        {
            previousPfxPassword = null;

            try
            {
                // Try to migrate old settings
                var oldPath = Path.Combine(
                    Environment.ExpandEnvironmentVariables("%ProgramData%"),
                    "ccsprivatekey");

                if (File.Exists(oldPath))
                {
                    var contents = File.ReadAllBytes(oldPath);
                    byte[] decryptedData = ProtectedData.Unprotect(contents, null, DataProtectionScope.LocalMachine);
                    string jsonData = System.Text.Encoding.UTF8.GetString(decryptedData);
                    previousPfxPassword = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(jsonData);
                    return true;
                }
            }
            catch (Exception e)
            {
            }

            return false;
        }
    }
}
