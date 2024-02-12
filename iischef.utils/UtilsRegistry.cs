using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace iischef.utils
{
    /// <summary>
    /// Revisado entero para usar powershell
    /// </summary>
    public static class UtilsRegistry
    {
        /// <summary>
        /// Get a value from Registry32
        /// </summary>
        /// <param name="hive"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        public static object GetRegistryKeyValue(RegistryHive hive, string key, string value, object defaultValue, RegistryView view)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                // PowerShell script to access the registry
                string script = @"
            param(
                [Microsoft.Win32.RegistryHive]$Hive,
                [string]$KeyPath,
                [string]$ValueName,
                [Microsoft.Win32.RegistryView]$View
            )

            try
            {
                $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, $View)
                $key = $baseKey.OpenSubKey($KeyPath)
                if ($key -eq $null)
                {
                    return $null
                }

                return $key.GetValue($ValueName)
            }
            catch
            {
                return $null
            }
        ";

                // Add the script and parameters to the PowerShell instance
                ps.AddScript(script)
                    .AddParameter("Hive", hive)
                    .AddParameter("KeyPath", key)
                    .AddParameter("ValueName", value)
                    .AddParameter("View", view);

                // Execute the script
                Collection<PSObject> results = ps.InvokeAndTreatError(null);

                // Return the result or the default value if the result is null
                return results.Count > 0 && results[0] != null ? results[0].BaseObject : defaultValue;
            }
        }

        /// <summary>
        /// Get a value from Registry64
        /// </summary>
        /// <param name="hive"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static object GetRegistryKeyValue64(RegistryHive hive, string key, string value, object defaultValue)
        {
            return GetRegistryKeyValue(hive, key, value, defaultValue, RegistryView.Registry64);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hive"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static object GetRegistryKeyValue32(RegistryHive hive, string key, string value, object defaultValue)
        {
            return GetRegistryKeyValue(hive, key, value, defaultValue, RegistryView.Registry32);
        }

        /// <summary>
        /// Set a registry value in both Registry32 and Registry64
        /// </summary>
        /// <param name="hive"></param>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="realValue"></param>
        /// <param name="valueKind"></param>
        public static void SetRegistryValue(RegistryHive hive, string key, string name, object realValue, RegistryValueKind valueKind)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                // PowerShell script to set the registry value
                string script = @"
            param(
                [Microsoft.Win32.RegistryHive]$Hive,
                [string]$KeyPath,
                [string]$ValueName,
                $RealValue,
                [Microsoft.Win32.RegistryValueKind]$ValueKind
            )

            function Set-RegistryValueForView([Microsoft.Win32.RegistryView]$View)
            {
                $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, $View)
                $key = $baseKey.OpenSubKey($KeyPath, $true)
                if ($key -eq $null)
                {
                    $key = $baseKey.CreateSubKey($KeyPath)
                }
                $key.SetValue($ValueName, $RealValue, $ValueKind)
            }

            Set-RegistryValueForView -View $([Microsoft.Win32.RegistryView]::Registry32)
            Set-RegistryValueForView -View $([Microsoft.Win32.RegistryView]::Registry64)
        ";

                // Add the script and parameters to the PowerShell instance
                ps.AddScript(script)
                    .AddParameter("Hive", hive)
                    .AddParameter("KeyPath", key)
                    .AddParameter("ValueName", name)
                    .AddParameter("RealValue", realValue)
                    .AddParameter("ValueKind", valueKind);

                // Execute the script
                ps.InvokeAndTreatError(null);
            }
        }
    }
}
