using iischef.logger;
using iischef.utils;
using System;

namespace iischef.core
{
    /// <summary>
    /// Cached version of IsWindowsFeatureEnabled
    /// </summary>
    public static class CoreUtils
    {
        public static bool IsWindowsFeatureEnabled(string featureName, ILoggerInterface logger)
        {
            var appSettings = new ApplicationDataStore();

            if (appSettings.AppSettings.FeatureEnabledLastSuccess?.TryGetValue(featureName, out var lastSuccess) == true && lastSuccess != null)
            {
                if ((DateTime.UtcNow - lastSuccess).Value.TotalDays < 30)
                {
                    return true;
                }
            }

            var result = UtilsSystem.IsWindowsFeatureEnabled(featureName, logger);

            if (result == true)
            {

                appSettings.AppSettings.FeatureEnabledLastSuccess = appSettings.AppSettings.FeatureEnabledLastSuccess ?? new System.Collections.Generic.Dictionary<string, DateTime?>();
                appSettings.AppSettings.FeatureEnabledLastSuccess[featureName] = DateTime.UtcNow;
                appSettings.Save();
            }

            return result;
        }
    }
}
