using iischef.logger;
using Microsoft.Web.Administration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Binding = Microsoft.Web.Administration.Binding;

namespace iischef.utils
{
    /// <summary>
    /// Random IIS management utilities
    /// </summary>
    public static class UtilsIis
    {
        public const string LOCALHOST_ADDRESS = "127.0.0.1";

        /// <summary>
        /// Stopwatch to meassure time since last iis configuration commit.
        /// </summary>
        public static Stopwatch StopwatchLastIisConfigCommmit = Stopwatch.StartNew();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

        /// <summary>
        /// Valid identity types for application pools
        /// </summary>
        public static List<string> ValidIdentityTypesForPool = new List<string>()
        {
            "SpecificUser",
            "LocalSystem",
            "LocalService",
            "NetworkService",
            "ApplicationPoolIdentity"
        };

        /// <summary>
        /// https://serverfault.com/questions/89245/how-to-move-c-inetpub-temp-apppools-to-another-disk
        /// https://support.microsoft.com/es-es/help/954864/description-of-the-registry-keys-that-are-used-by-iis-7-0-iis-7-5-and
        /// </summary>
        /// <returns></returns>
        public static string GetConfigIsolationPath()
        {
            var path = Convert.ToString(UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "System\\CurrentControlSet\\Services\\WAS\\Parameters",
                "ConfigIsolationPath",
                (string)null));

            if (string.IsNullOrWhiteSpace(path))
            {
                path = Environment.ExpandEnvironmentVariables("%systemdrive%\\inetpub\\temp\\apppools");
            }

            return path;
        }

        /// <summary>
        /// Use the pool identity
        /// </summary>
        /// <param name="siteName"></param>
        public static void ConfigureAnonymousAuthForIisApplicationToUsePool(string siteName)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection section;

                section = config.GetSection("system.webServer/security/authentication/anonymousAuthentication", siteName);

                bool changed = false;

                section.EnsureElementAttributeValue("enabled", true, ref changed);
                section.EnsureElementAttributeValue("userName", string.Empty, ref changed);
                section.EnsureElementAttributeValue("password", string.Empty, ref changed);

                if (changed)
                {
                    UtilsIis.CommitChanges(serverManager);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverManager"></param>
        /// <param name="poolName"></param>
        /// <returns>SpecificUser || LocalSystem || LocalService || NetworkService || ApplicationPoolIdentity</returns>
        public static string FindIdentityTypeForPool(ServerManager serverManager, string poolName)
        {
            Configuration config = serverManager.GetApplicationHostConfiguration();

            ConfigurationSection applicationPoolsSection = config.GetSection("system.applicationHost/applicationPools");

            ConfigurationElementCollection applicationPoolsCollection = applicationPoolsSection.GetCollection();

            ConfigurationElement addElement = FindElement(applicationPoolsCollection, "add", "name", poolName);
            if (addElement == null)
            {
                throw new InvalidOperationException("Element not found!");
            }

            ConfigurationElement processModelElement = addElement.GetChildElement("processModel");
            return (string)processModelElement["identityType"];
        }

        /// <summary>
        /// If the current server version supports the IdleTimeoutAction pool attribute
        /// </summary>
        /// <returns></returns>
        public static bool IisSupportsIdleTimeoutAction()
        {
            var iisVersion = UtilsIis.GetIisVersion();
            return iisVersion >= Version.Parse("8.5");
        }

        public static void UpsertPoolEnv(string pool, Dictionary<string, string> env)
        {
            UtilsIis.ServerManagerRetry((manager) =>
            {
                var poolInstance = manager.ApplicationPools.FirstOrDefault((i) => i.Name == pool);

                if (poolInstance == null)
                {
                    throw new BusinessRuleException($"Pool {pool} not found.");
                }

                Microsoft.Web.Administration.Configuration config = manager.GetApplicationHostConfiguration();
                Microsoft.Web.Administration.ConfigurationSection section = config.GetSection("system.applicationHost/applicationPools");
                Microsoft.Web.Administration.ConfigurationElement cfs = null;

                foreach (Microsoft.Web.Administration.ConfigurationElement sec in section.GetCollection())
                {
                    // Cada aplicación se identifica de manera única por la combincación de atributo y path de ejecución.
                    if (ExtensionUtils.HasValue(sec, "name", pool))
                    {
                        cfs = sec;
                        break;
                    }
                }

                Microsoft.Web.Administration.ConfigurationElement cfgEnvironment =
                    cfs.GetChildElement("environmentVariables");
                Microsoft.Web.Administration.ConfigurationElementCollection a = cfgEnvironment.GetCollection();

                foreach (var e in env)
                {
                    try
                    {
                        a.UpsertNameValueElementInCollection(e.Key, e.Value, string.Empty);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            $"Unable to set environment variable '{e.Key}' with value '{e.Value}'",
                            ex);
                    }
                }

                manager.CommitChanges();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="timeoutAction"></param>
        /// <param name="logger"></param>
        public static void SetIisPoolIdleTimeoutAction(ApplicationPool pool, IdleTimeoutAction? timeoutAction, ILoggerInterface logger)
        {
            if (timeoutAction == null)
            {
                return;
            }

            // Feature introduced in IIS 8.5
            if (IisSupportsIdleTimeoutAction())
            {
                pool.ProcessModel.IdleTimeoutAction = timeoutAction.Value;
            }
            else
            {
                var iisVersion = UtilsIis.GetIisVersion();
                logger.LogInfo(false, $"Current IIS version {iisVersion} does not support IdleTimeoutAction");
            }
        }

        /// <summary>
        /// Server manager maniuplation is prompt to concurrency, in those
        /// cases, retry a few times before actually failing
        /// </summary>
        /// <param name="action"></param>
        public static void ServerManagerRetry(Action<ServerManager> action)
        {
            int maxRetries = 3;
            int retryCount = 0;

            while (true)
            {
                try
                {
                    using (ServerManager sm = new ServerManager())
                    {
                        action(sm);
                        return;
                    }
                }
                catch (FileLoadException e) when (e.Message.Contains("applicationHost.config") || e.Message.Contains("web.config"))
                {
                    if (retryCount >= maxRetries)
                    {
                        throw;
                    }

                    System.Threading.Thread.Sleep(250);
                    retryCount++;
                }
            }
        }

        /// <summary>
        /// Busca la carpeta de user settings para application pools
        /// que están configurados como "ApplicationPoolIdentity".
        /// </summary>
        /// <param name="pool"></param>
        /// <returns></returns>
        public static string FindStorageFolderForAppPoolWithDefaultIdentity(ApplicationPool pool)
        {
            // Peligroso... este método solo para las identidades de defecto.
            if (pool.ProcessModel.IdentityType != ProcessModelIdentityType.ApplicationPoolIdentity)
            {
                return null;
            }

            // If the user profile is not being loaded, then no need
            // to handle this.
            if (pool.ProcessModel.LoadUserProfile == false)
            {
                return null;
            }

            var suffix = "\\" + Environment.UserName;
            var pathWithEnv = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%");
            if (!pathWithEnv.EndsWith(suffix))
            {
                return null;
            }

            var subpath = pathWithEnv.Substring(0, pathWithEnv.Length - suffix.Length);
            var userpath = UtilsSystem.CombinePaths(subpath, pool.Name + ".IIS APPPOOL");

            if (Directory.Exists(userpath))
            {
                return userpath;
            }

            userpath = UtilsSystem.CombinePaths(subpath, pool.Name);
            if (Directory.Exists(userpath))
            {
                return userpath;
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="elementTagName"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        private static ConfigurationElement FindElement(
            ConfigurationElementCollection collection,
            string elementTagName,
            params string[] keyValues)
        {
            foreach (ConfigurationElement element in collection)
            {
                if (string.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    bool matches = true;

                    for (int i = 0; i < keyValues.Length; i += 2)
                    {
                        object o = element.GetAttributeValue(keyValues[i]);
                        string value = null;
                        if (o != null)
                        {
                            value = o.ToString();
                        }

                        if (!string.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the current IIS version
        /// </summary>
        /// <returns></returns>
        public static Version GetIisVersion()
        {
            using (PowerShell ps = PowerShell.Create())
            {
                // PowerShell script to get the IIS version from the registry
                string script = @"
            $keyPath = 'HKLM:\Software\Microsoft\InetStp'
            $key = Get-ItemProperty -Path $keyPath -ErrorAction SilentlyContinue
            if ($key) {
                return [string]::Format('{0}.{1}', $key.MajorVersion, $key.MinorVersion)
            } else {
                return '0.0'
            }
        ";

                // Add the script to the PowerShell instance
                ps.AddScript(script);

                // Execute the script
                Collection<PSObject> results = ps.Invoke();

                // Check for errors
                if (ps.Streams.Error.Count > 0)
                {
                    // Handle errors here
                    return new Version(0, 0);
                }

                // Parse the result as a Version object
                if (results.Count > 0)
                {
                    string versionString = results[0].BaseObject as string;
                    return Version.Parse(versionString);
                }

                return new Version(0, 0);
            }
        }

        /// <summary>
        /// Write contents of a web.config.
        ///
        /// Takes into consideration maximum web.config file size as defined at the OS level.
        ///
        /// Throws an exception if size is exceeded.
        /// </summary>
        /// <param name="webconfigfilepath"></param>
        /// <param name="webConfigContents"></param>
        /// <returns></returns>
        public static void WriteWebConfig(string webconfigfilepath, string webConfigContents)
        {
            long requiredSizeKb = UtilsSystem.GetStringSizeInDiskBytes(webConfigContents) / 1024;
            int maxSizeKb = UtilsIis.GetMaxWebConfigFileSizeInKb() - 1;

            if (requiredSizeKb >= maxSizeKb)
            {
                throw new Exception($"Required web.config size of {requiredSizeKb}Kb for CDN chef feature exceeds current limit of {maxSizeKb}Kb. Please, review this in 'HKLM\\SOFTWARE\\Microsoft\\InetStp\\Configuration\\MaxWebConfigFileSizeInKB'");
            }

            File.WriteAllText(webconfigfilepath, webConfigContents);
        }

        /// <summary>
        /// Remove a site and treat bindings gracefully
        /// </summary>
        /// <param name="site"></param>
        /// <param name="manager"></param>
        /// <param name="logger"></param>
        public static void RemoveSite(Site site, ServerManager manager, ILoggerInterface logger)
        {
            RemoveSiteBindings(site, manager, (i) => i.Protocol == "https", logger);
            manager.Sites.Remove(site);
        }

        /// <summary>
        /// Delete a site from the server manager.
        /// 
        /// The behaviour has been enhanced to prevent deleting a site from affecting bindings from
        /// other sites. See related article. Doing a simple sites.Remove() call can totally mess up bindings from
        /// other websites, which becomes even worse when using CCS (central certificate store).
        /// 
        /// @see https://stackoverflow.com/questions/37792421/removing-secured-site-programmatically-spoils-other-bindings
        /// </summary>
        /// <param name="site"></param>
        /// <param name="manager"></param>
        /// <param name="criteria"></param>
        /// <param name="logger"></param>
        public static void RemoveSiteBindings(
            Site site,
            ServerManager manager,
            Func<Binding, bool> criteria,
            ILoggerInterface logger)
        {
            // Site bindings
            var siteBindingsToRemove = site.Bindings.Where(criteria).ToList();

            // Make sure that this is "resilient"
            var allBindings = UtilsSystem.QueryEnumerable(
                manager.Sites,
                (s) => true,
                (s) => s.Bindings,
                (s) => s.Name,
                logger);

            // We have to collect all existing bindings from other sites, including ourse
            var existingBindings = (from p in allBindings
                                    select p.Where((i) => i.Protocol == "https")).SelectMany((i) => i)
                .ToList();

            // Remove all bindings for our site, we only care about SSL bindings,the other onse
            // will be removed with the site itself
            foreach (var b in siteBindingsToRemove)
            {
                existingBindings.Remove(b);

                // The central certificate store
                bool bindingUsedInAnotherSite = (from p in existingBindings
                                                 where (p.Host == b.Host
                                                       && p.BindingInformation == b.BindingInformation)
                                                       ||

                                                       // A combination of port and usage of CCS is a positive, even if in different IP addresses
                                                       (p.SslFlags.HasFlag(SslFlags.CentralCertStore) && b.SslFlags.HasFlag(SslFlags.CentralCertStore)
                                                           && p.EndPoint.Port.ToString() == b.EndPoint.Port.ToString())
                                                 select 1).Any();

                site.Bindings.Remove(b, bindingUsedInAnotherSite);
            }
        }

        /// <summary>
        /// Bound certificate info
        /// </summary>
        public class BoundCertificate
        {
            public Dictionary<string, string> Attributes { get; set; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static List<BoundCertificate> GetBoundCertificates()
        {
            List<BoundCertificate> result = new List<BoundCertificate>();
            string outputTool = string.Empty;

            try
            {
                using (Process tool = new Process())
                {
                    tool.StartInfo.FileName = "netsh";
                    tool.StartInfo.Arguments = "http show sslcert";
                    tool.StartInfo.UseShellExecute = false;
                    tool.StartInfo.Verb = "runas";
                    tool.StartInfo.RedirectStandardOutput = true;
                    tool.Start();

                    outputTool = tool.StandardOutput.ReadToEnd();

                    tool.Close();
                    tool.Kill();
                }
            }
            catch
            {
                // ignored
            }

            var lines = outputTool.Split(Environment.NewLine.ToCharArray());
            BoundCertificate currentCert = null;
            bool eol = false;
            int wsCount = 0;

            foreach (var line in lines)
            {
                if (!eol)
                {
                    if (line.Contains("-------------------------"))
                    {
                        eol = true;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Trim()))
                {
                    wsCount++;

                    if (wsCount > 2)
                    {
                        wsCount = 0;

                        if (currentCert != null)
                        {
                            result.Add(currentCert);
                        }

                        currentCert = new BoundCertificate();
                        currentCert.Attributes = new Dictionary<string, string>();
                    }

                    continue;
                }

                wsCount = 0;

                if (line.Length < 30)
                {
                    continue;
                }

                int breakPoint = line.IndexOf(":", 30);

                if (breakPoint == -1)
                {
                    continue;
                }

                var attributeName = line.Substring(0, breakPoint).Trim();
                var attributeValue = line.Substring(breakPoint + 1, line.Length - breakPoint - 1).Trim();

                if (currentCert.Attributes.ContainsKey(attributeName))
                {
                    continue;
                }

                currentCert.Attributes.Add(attributeName, attributeValue);
            }

            result.Add(currentCert);

            return result.Where((i) => i.Attributes.Any()).ToList();
        }

        /// <summary>
        /// Check if central certificate store is enabled
        /// </summary>
        /// <returns></returns>
        public static bool CentralStoreEnabled()
        {
            // We need to read the registry to check that this is enabled
            // in IIS and that the path is properly configured
            int.TryParse(
                UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS\\CentralCertProvider",
                "Enabled",
                0)?.ToString(), out int enabled);

            return enabled == 1;
        }

        /// <summary>
        /// Check if central certificate store is enabled
        /// </summary>
        /// <returns></returns>
        public static string GetUrlRewriteVersion()
        {
            // We need to read the registry to check that this is enabled
            // in IIS and that the path is properly configured
            var version = UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS Extensions\\URL Rewrite",
                "Version",
                null)?.ToString();

            return version;
        }

        /// <summary>
        /// Get version of Application Request Routing
        /// </summary>
        /// <returns></returns>
        public static string GetArrVersion()
        {
            // We need to read the registry to check that this is enabled
            // in IIS and that the path is properly configured
            var version = UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS Extensions\\Application Request Routing",
                "Version",
                null)?.ToString();

            return version;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string GetExternalDiskCachingVersion()
        {
            // We need to read the registry to check that this is enabled
            // in IIS and that the path is properly configured
            var version = UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS Extensions\\External Disk Cache",
                "Version",
                null)?.ToString();

            return version;
        }

        /// <summary>
        /// Returns currently configured central store location, without any checks.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static string GetCentralCertificateStorePathRaw(ILoggerInterface logger)
        {
            string certStoreLocation = Convert.ToString(UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS\\CentralCertProvider",
                "CertStoreLocation",
                string.Empty));

            return certStoreLocation;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static string GetCentralCertificateStoreUserNameRaw(ILoggerInterface logger)
        {
            string certStoreLocation = Convert.ToString(UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS\\CentralCertProvider",
                "UserName",
                string.Empty));

            return certStoreLocation;
        }

        /// <summary>
        /// Central store path for certificates. Returns exception if not configured or cannot be returned.
        /// </summary>
        public static string GetCentralCertificateStorePath(ILoggerInterface logger)
        {
            if (!CentralStoreEnabled())
            {
                throw new Exception(
                    "IIS Central store path not enabled or installed. Please check https://blogs.msdn.microsoft.com/kaushal/2012/10/11/central-certificate-store-ccs-with-iis-8-windows-server-2012/");
            }

            string certStoreLocation = Convert.ToString(UtilsRegistry.GetRegistryKeyValue64(
                RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\IIS\\CentralCertProvider",
                "CertStoreLocation",
                string.Empty));

            if (string.IsNullOrWhiteSpace(certStoreLocation))
            {
                throw new Exception("IIS Central store location not configured");
            }

            if (!Directory.Exists(certStoreLocation))
            {
                return certStoreLocation;
            }

            var resolvedCertStoreLocation = certStoreLocation;

            if (UtilsJunction.IsJunctionOrSymlink(certStoreLocation))
            {
                resolvedCertStoreLocation = UtilsJunction.ResolvePath(resolvedCertStoreLocation);
            }

            if (UtilsSystem.IsNetworkPath(resolvedCertStoreLocation))
            {
                logger.LogWarning(true, "Central Certificate Store Path is located on a network share [{0}]. This has proven to be unstable as CCS will cache corrupted certificates when it is unable to read from the network share.", certStoreLocation);
            }

            return certStoreLocation;
        }

        /// <summary>
        /// IIS has been historically very sensible to quick configuration changes
        /// on the same server. Commiting changes too fast can lead to temporary corruption
        /// (i.e. iis restart needed). This commit wrapper methods ensure that a minimum
        /// time has lapsed since last time the server configuration changed.
        /// </summary>
        /// <param name="sm"></param>
        public static void CommitChanges(ServerManager sm)
        {
            var sleepTime = (int)(1000 - StopwatchLastIisConfigCommmit.ElapsedMilliseconds);

            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }

            sm.CommitChanges();
            StopwatchLastIisConfigCommmit.Restart();
        }

        /// <summary>
        /// Find a site with a specific name in a resilient way
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="siteName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static List<Site> FindSiteWithName(ServerManager manager, string siteName, ILoggerInterface logger)
        {
            return UtilsSystem.QueryEnumerable(
                manager.Sites,
                (s) => s.Name == siteName,
                (s) => s,
                (s) => s.Name,
                logger);
        }

        /// <summary>
        /// There is a delay between a serverManager.CommitChanges() and the actual
        /// materialization of the configuration.
        ///
        /// This methods waits for a specific site to be available.
        /// </summary>
        public static void WaitForSiteToBeAvailable(string siteName, ILoggerInterface logger)
        {
            UtilsSystem.RetryWhile(
                () =>
                {
                    using (ServerManager sm = new ServerManager())
                    {
                        // Site is ready when state is available
                        var state = UtilsSystem.QueryEnumerable(
                            sm.Sites,
                            (s) => s.Name == siteName,
                            (s) => s,
                            (s) => s.Name,
                            logger).Single().State;
                    }

                    return true;
                },
                (e) => true,
                3000,
                logger);
        }

        /// <summary>
        /// Check a site exists without SNI when there is an CSS
        /// </summary>
        public static void CheckSitesWithoutSni(ILoggerInterface logger)
        {
            using (ServerManager manager = new ServerManager(true, (string)null))
            {
                bool anySitesHasCcs = (from p in manager.Sites
                                       where p.Bindings.Any(o => o.Protocol == "https" && o.SslFlags.HasFlag(SslFlags.CentralCertStore))
                                       select 1).Any();

                if (anySitesHasCcs)
                {
                    var sitesWithoutSni = (from p in manager.Sites
                                           where p.Bindings.Any(o => o.Protocol == "https" && !o.SslFlags.HasFlag(SslFlags.Sni))
                                           select p.Name).ToList();

                    if (sitesWithoutSni.Any())
                    {
                        logger.LogError(string.Concat("There is a site using CSS (CentralCertStore) and the following sites don't have SNI enabled (Server Name Indication): ", string.Join(", ", sitesWithoutSni)));
                    }
                }
            }
        }

        /// <summary>
        /// Throw an exception if hostname is not valid
        /// </summary>
        /// <param name="hostName"></param>
        public static string ValidateHostName(string hostName)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                return hostName;
            }

            int maxHostNameLength = 253;
            var maxPartNameLength = 63;

            // 253 characters is the maximum length of full domain name, including dots: e.g.www.example.com = 15 characters.
            // 63 characters in the maximum length of a "label"(part of domain name separated by dot).Labels for www.example.com are com, example and www.

            if (hostName.Length >= maxHostNameLength)
            {
                throw new Exception($"Invalid hostname '{hostName}' exceeds maximum length of {maxHostNameLength}");
            }

            var parts = hostName.Split(".".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            var invalidParts = (from p in parts where p.Length >= maxPartNameLength select p).ToList();

            if (invalidParts.Any())
            {
                throw new Exception(
                    $"Invalid hostname parts exceed maximum length {maxPartNameLength} '{string.Join(", ", invalidParts)}'");
            }

            return hostName;
        }

        /// <summary>
        /// Get the maximum web.config file size in KB
        /// </summary>
        /// <returns></returns>
        public static int GetMaxWebConfigFileSizeInKb()
        {
            // https://stackoverflow.com/questions/3972728/web-config-size-limit-exceeded-under-iis7-0x80070032
            ////Have you tried adding this registry key:

            ////HKLM\SOFTWARE\Microsoft\InetStp\Configuration

            ////    Then set this DWORD value: MaxWebConfigFileSizeInKB

            ////    If your system is running 64 bit windows but your application pool is running in 32 - bit mode then you may need to set this in:

            ////HKLM\SOFTWARE\Wow6232Node\Microsoft\InetStp\Configuration

            using (RegistryKey componentsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\InetStp\Configuration", false))
            {
                if (componentsKey != null)
                {
                    return (int)componentsKey.GetValue("MaxWebConfigFileSizeInKB", 250);
                }

                return 250;
            }
        }

        public static Tuple<IPAddress, IPAddress> GetSubnetAndMaskFromCidr(string cidr)
        {
            var delimiterIndex = cidr.IndexOf('/');

            if (delimiterIndex == -1)
            {
                return new Tuple<IPAddress, IPAddress>(IPAddress.Parse(cidr), null);
            }

            string ipSubnet = cidr.Substring(0, delimiterIndex);
            string mask = cidr.Substring(delimiterIndex + 1);

            var subnetAddress = IPAddress.Parse(ipSubnet);

            if (subnetAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // ipv6
                var ip = BigInteger.Parse("00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", NumberStyles.HexNumber) << (128 - int.Parse(mask));

                var maskBytes = new[]
                {
                    (byte)((ip & BigInteger.Parse("00FF000000000000000000000000000000", NumberStyles.HexNumber)) >> 120),
                    (byte)((ip & BigInteger.Parse("0000FF0000000000000000000000000000", NumberStyles.HexNumber)) >> 112),
                    (byte)((ip & BigInteger.Parse("000000FF00000000000000000000000000", NumberStyles.HexNumber)) >> 104),
                    (byte)((ip & BigInteger.Parse("00000000FF000000000000000000000000", NumberStyles.HexNumber)) >> 96),
                    (byte)((ip & BigInteger.Parse("0000000000FF0000000000000000000000", NumberStyles.HexNumber)) >> 88),
                    (byte)((ip & BigInteger.Parse("000000000000FF00000000000000000000", NumberStyles.HexNumber)) >> 80),
                    (byte)((ip & BigInteger.Parse("00000000000000FF000000000000000000", NumberStyles.HexNumber)) >> 72),
                    (byte)((ip & BigInteger.Parse("0000000000000000FF0000000000000000", NumberStyles.HexNumber)) >> 64),
                    (byte)((ip & BigInteger.Parse("000000000000000000FF00000000000000", NumberStyles.HexNumber)) >> 56),
                    (byte)((ip & BigInteger.Parse("00000000000000000000FF000000000000", NumberStyles.HexNumber)) >> 48),
                    (byte)((ip & BigInteger.Parse("0000000000000000000000FF0000000000", NumberStyles.HexNumber)) >> 40),
                    (byte)((ip & BigInteger.Parse("000000000000000000000000FF00000000", NumberStyles.HexNumber)) >> 32),
                    (byte)((ip & BigInteger.Parse("00000000000000000000000000FF000000", NumberStyles.HexNumber)) >> 24),
                    (byte)((ip & BigInteger.Parse("0000000000000000000000000000FF0000", NumberStyles.HexNumber)) >> 16),
                    (byte)((ip & BigInteger.Parse("000000000000000000000000000000FF00", NumberStyles.HexNumber)) >> 8),
                    (byte)((ip & BigInteger.Parse("00000000000000000000000000000000FF", NumberStyles.HexNumber)) >> 0),
                };

                return Tuple.Create(subnetAddress, new IPAddress(maskBytes));
            }
            else
            {
                // ipv4
                uint ip = 0xFFFFFFFF << (32 - int.Parse(mask));

                var maskBytes = new[]
                {
                    (byte)((ip & 0xFF000000) >> 24),
                    (byte)((ip & 0x00FF0000) >> 16),
                    (byte)((ip & 0x0000FF00) >> 8),
                    (byte)((ip & 0x000000FF) >> 0),
                };

                return Tuple.Create(subnetAddress, new IPAddress(maskBytes));
            }
        }

        /// <summary>
        /// Add a list of allowed server variables
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="variables">List of headers</param>
        public static void AddAllowedServerVariablesForUrlRewrite(string siteName, params string[] variables)
        {
            if (variables.Length == 0)
            {
                return;
            }

            UtilsIis.ServerManagerRetry((serverManager) =>
            {
                bool changed = false;

                Configuration config = serverManager.GetApplicationHostConfiguration();

                ConfigurationSection allowedServerVariablesSection = config.GetSection("system.webServer/rewrite/allowedServerVariables", siteName);

                ConfigurationElementCollection allowedServerVariablesCollection = allowedServerVariablesSection.GetCollection();

                List<string> existingVariables = new List<string>();

                foreach (ConfigurationElement e in allowedServerVariablesCollection)
                {
                    if (variables.Any((i) => i.Equals(Convert.ToString(e["name"]), StringComparison.CurrentCultureIgnoreCase)))
                    {
                        existingVariables.Add(Convert.ToString(e["name"]));
                    }
                }

                var missingVariables = variables.Except(existingVariables).ToList();

                if (!missingVariables.Any())
                {
                    return;
                }

                foreach (var missingVariable in missingVariables)
                {
                    changed = true;
                    ConfigurationElement addElement = allowedServerVariablesCollection.CreateElement("add");
                    addElement["name"] = missingVariable;
                    allowedServerVariablesCollection.Add(addElement);
                }

                if (changed)
                {
                    UtilsIis.CommitChanges(serverManager);
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostName">The hostname you want to match to such as "www.google.com"</param>
        /// <param name="target">The target name to match against, can be an exact domain or a wildcarded hostname (*.google.com)</param>
        /// <param name="targetWidcardChar">Character used as wildcard in target match: _ in IIS CCS filenames, * otherwise </param>
        /// <returns></returns>
        public static bool HostNameMatch(string hostName, string target, string targetWidcardChar)
        {
            var hostNameParts = hostName.Split(".".ToCharArray()).Reverse().ToList();

            // Check if this certificate file is valid for the hostname...
            var certNameParts = target.Split(".".ToCharArray()).Reverse()
                .ToList();

            // This won't allow for nested subdomain with wildcards, but it's a good starting point
            // i.e. a hostname such as "a.mytest.mydomain.com" won't be matched to a certifica
            // such as "_.mydomain.com"
            // but "mytest.mydomain.com" will match to "_.mydomain.com".
            if (certNameParts.Count != hostNameParts.Count)
            {
                return false;
            }

            for (int x = 0; x < hostNameParts.Count; x++)
            {
                // Certname parts have two type of wildcards, those used in filenames for IIS (_), and
                // those used in subject names in certificates themselves ("*")
                if (hostNameParts[x] == "*" || (certNameParts[x] == targetWidcardChar))
                {
                    continue;
                }

                if (!string.Equals(hostNameParts[x], certNameParts[x], StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a DNS record exists for the given hostname.
        /// </summary>
        /// <param name="hostname">The hostname to check.</param>
        /// <returns>True if a DNS record exists, False otherwise.</returns>
        public static bool CheckHostnameDNS(string hostname)
        {
            try
            {
                // Attempt to resolve the DNS entry for the given hostname
                IPHostEntry hostEntry = Dns.GetHostEntry(hostname);

                // If the resolution succeeds and we get an IP address, a DNS record exists
                return hostEntry.AddressList.Length > 0;
            }
            catch (SocketException)
            {
                // DNS resolution failed, meaning no DNS record exists for the hostname
                return false;
            }
            catch (ArgumentException)
            {
                // The hostname provided was invalid (e.g., null or empty)
                Console.WriteLine("Invalid hostname provided.");
                return false;
            }
        }

        /// <summary>
        /// Find a certificate in IIS central certificate store
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="logger"></param>
        /// <param name="certificatePassword"></param>
        /// <param name="certificatePath"></param>
        /// <returns></returns>
        public static X509Certificate2 FindCertificateInCentralCertificateStore(
            string hostName,
            ILoggerInterface logger,
            string certificatePassword,
            out string certificatePath)
        {
            string centralStorePath = UtilsIis.GetCentralCertificateStorePath(logger);

            if (!Directory.Exists(centralStorePath))
            {
                throw new BusinessRuleException($"Configured central certificate store path {centralStorePath} does not exist.");
            }

            FileInfo certificateFile = null;

            foreach (var f in new DirectoryInfo(centralStorePath).EnumerateFiles("*.pfx"))
            {
                // Look for a certificate file that includes wildcard matching logic
                // https://serverfault.com/questions/901494/iis-wildcard-https-binding-with-centralized-certificate-store
                var targetHost = Path.GetFileNameWithoutExtension(f.FullName);

                if (HostNameMatch(hostName, targetHost, "_"))
                {
                    certificateFile = f;
                    break;
                }
            }

            certificatePath = certificateFile?.FullName;

            X509Certificate2Collection collection = new X509Certificate2Collection();

            if (certificateFile != null)
            {
                logger.LogInfo(true, "Found potential certificate matching file at {0}", certificateFile.FullName);

                try
                {
                    // Usamos ephemeral keyset para que no almacene claves en la máquina todo el tiempo...
                    collection.Import(certificateFile.FullName, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);

                    // A certificate file might contain a collection of certificates, we need to find
                    // the exactly matching one
                    logger.LogInfo(true, "Certificate file contains {0} certificates", collection.Count);

                    int x = 0;

                    foreach (var certificate in collection)
                    {
                        var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
                        var alternativeNames = certificate.GetNameInfo(X509NameType.DnsName, false);

                        logger.LogInfo(true, $"{x} Certificate Names '{commonName}' || '{alternativeNames}'");
                        logger.LogInfo(true, $"{x} Certificate IssuerName '{certificate.IssuerName.Name}'");

                        // logger.LogInfo(true, "Certificate FriendlyName '{0}'", certificate.FriendlyName);
                        logger.LogInfo(true, $"{x} Certificate SubjectName '{certificate.SubjectName.Name}'");

                        // logger.LogInfo(true, "Certificate NotBefore '{0}'", certificate.NotBefore.ToString("yyyy/MM/dd HH:mm:ss"));
                        logger.LogInfo(true, $"{x} Certificate NotAfter '{certificate.NotAfter.ToString("yyyy/MM/dd HH:mm:ss")}'");

                        if (HostNameMatch(hostName, commonName, "*"))
                        {
                            logger.LogInfo(true, $"{x} Common name {commonName} match for hostname {hostName}");
                            return certificate;
                        }

                        if (HostNameMatch(hostName, alternativeNames, "*"))
                        {
                            logger.LogInfo(true, $"{x} Common name {alternativeNames} match for alternative hostname {hostName}");
                            return certificate;
                        }

                        x++;
                    }
                }
                catch (Exception e)
                {
                    logger.LogWarning(false, $"Error reading certificate details '{certificateFile.FullName}' " + e.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Obtiene, para cada binding, todos los sitios donde aparece. Es para encontrar duplicados.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="regexHost"></param>
        /// <param name="regexProtocol"></param>
        /// <returns>Diccionario con las claves las configuraciones de bindings, y los valores el listado de sitios donde se usa el binding</returns>
        public static List<BindingSites> ParseSiteBindings(
            ILoggerInterface logger,
            string regexHost = null,
            string regexProtocol = null)
        {
            var result = new Dictionary<string, BindingSites>();

            Dictionary<string, BindingSiteInfo> siteInfo = new Dictionary<string, BindingSiteInfo>();

            using (ServerManager sm = new ServerManager())
            {
                foreach (var site in sm.Sites)
                {
                    foreach (var siteBinding in site.Bindings)
                    {
                        var bindingDefinition = siteBinding.BindingInformation;
                        var definition = new BindingDefinition(siteBinding);

                        if (!string.IsNullOrWhiteSpace(regexHost) && !Regex.IsMatch(definition.Hostname, regexHost, RegexOptions.IgnoreCase))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(regexProtocol) && !Regex.IsMatch(definition.Protocol, regexProtocol, RegexOptions.IgnoreCase))
                        {
                            continue;
                        }

                        if (!result.ContainsKey(bindingDefinition))
                        {
                            result.Add(bindingDefinition, new BindingSites()
                            {
                                Binding = definition
                            });
                        }

                        ObjectState siteState = ObjectState.Unknown;

                        try
                        {
                            siteState = site.State;
                        }
                        catch (COMException e)
                        {
                            // Sites might be in a bad state/hanged, asume they are in
                            // unkown state.
                        }

                        result[bindingDefinition].Sites.Add(new BindingSiteInfo()
                        {
                            Name = site.Name,
                            State = siteState
                        });
                    }
                }
            }

            return result.Values.ToList();
        }

        /// <summary>
        /// Relacion entre binding y sites
        /// </summary>
        public class BindingSites
        {
            public BindingDefinition Binding { get; set; }

            public List<BindingSiteInfo> Sites { get; set; } = new List<BindingSiteInfo>();
        }

        /// <summary>
        /// 
        /// </summary>
        public class BindingSiteInfo
        {
            public string Name { get; set; }

            public ObjectState State { get; set; }
        }

        /// <summary>
        /// Parseador de bindings formato IIS
        /// </summary>
        public class BindingDefinition
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="b"></param>
            public BindingDefinition(Binding b)
            {
                this.Binding = b.BindingInformation;
                var parts = this.Binding.Split(":".ToCharArray());
                this.IpAddress = parts[0];
                this.Port = parts[1];
                this.Hostname = parts[2];
                this.Protocol = b.Protocol;
                this.SslFlags = b.SslFlags;
                this.CertificateHash = b.CertificateHash;
                this.StoreName = b.CertificateStoreName;
                this.UseDsMapper = b.UseDsMapper;
            }

            public string Binding { get; private set; }

            public string IpAddress { get; private set; }

            public string Port { get; private set; }

            public string Hostname { get; private set; }

            public string Protocol { get; private set; }

            public SslFlags SslFlags { get; private set; }

            public string StoreName { get; private set; }

            public byte[] CertificateHash { get; private set; }

            public bool UseDsMapper { get; private set; }
        }

        /// <summary>
        /// Enable IIS-ARR proxy configuration, required for centralized CDN
        /// and centralized ACME challenge validation
        /// </summary>
        public static void ConfigureProxy(ILoggerInterface logger)
        {
            var applicationRequestRoutingVersion = UtilsIis.GetArrVersion();

            if (string.IsNullOrWhiteSpace(applicationRequestRoutingVersion))
            {
                logger.LogException(new Exception("Application request routing package must be installed.", null));
                return;
            }

            // Ensure that proxy is enabled and available at the IIS level.
            // This needs the IIS Application Request Routing extension.
            using (ServerManager manager = new ServerManager())
            {
                bool configChanged = false;

                var config = manager.GetApplicationHostConfiguration();

                ConfigurationSection proxySection = config.GetSection("system.webServer/proxy");

                // Disable reverseRewriteHostInResponseHeaders
                proxySection.EnsureElementAttributeValue("reverseRewriteHostInResponseHeaders", false, ref configChanged);

                // Enable proxy functionality
                proxySection.EnsureElementAttributeValue("enabled", true, ref configChanged);

                // Disable disk cache
                ConfigurationElement cacheElement = proxySection.GetChildElement("cache");
                cacheElement.EnsureElementAttributeValue("enabled", false, ref configChanged);

                if (configChanged)
                {
                    logger.LogWarning(false, "Your IIS-ARR settings have been updated to enable request proxying.");
                    UtilsIis.CommitChanges(manager);
                }
            }
        }

        /// <summary>
        /// Add a binding to a site if it's missing
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="bindingInfo"></param>
        /// <param name="bindingProtocol"></param>
        public static void AddHttpBindingToSite(string siteName, string bindingInfo, string bindingProtocol = "http")
        {
            using (ServerManager sm = new ServerManager())
            {
                var site = (from p in sm.Sites
                            where p.Name == siteName
                            select p).FirstOrDefault();

                if (site == null)
                {
                    throw new ArgumentException("Unable to find site with name: " + siteName);
                }

                var binding = site.Bindings.Where((i) =>
                    i.BindingInformation == bindingInfo && i.Protocol == bindingProtocol);

                if (binding.Any())
                {
                    return;
                }

                site.Bindings.Add(bindingInfo, "http");

                UtilsIis.CommitChanges(sm);
            }
        }

        /// <summary>
        /// Remove a binding from a site if it exists
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="bindingInfo"></param>
        /// <param name="bindingProtocol"></param>
        public static void RemoveHttpBindingFromSite(string siteName, string bindingInfo, string bindingProtocol = "http")
        {
            using (ServerManager sm = new ServerManager())
            {
                var site = (from p in sm.Sites
                            where p.Name == siteName
                            select p).FirstOrDefault();

                if (site == null)
                {
                    throw new ArgumentException("Unable to find site with name: " + siteName);
                }

                var binding = site.Bindings.Where((i) =>
                    i.BindingInformation == bindingInfo && i.Protocol == bindingProtocol).ToList();

                if (binding.Any())
                {
                    site.Bindings.Remove(binding.First());
                }

                UtilsIis.CommitChanges(sm);
            }
        }

        /// <summary>
        /// Cleanup all stale fast cgi configurations
        /// </summary>
        /// <param name="logger"></param>
        public static void CleanUpFastCgiSettings(ILoggerInterface logger)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                bool changesMade = false;
                Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection section = config.GetSection("system.webServer/fastCgi");
                ConfigurationElementCollection fastCgiHandlers = section.GetCollection();

                // Cleanup any fastCgi applications that point to non-existent handlers
                foreach (ConfigurationElement sec in fastCgiHandlers.ToList())
                {
                    if (sec.RawAttributes.Keys.Contains("fullPath"))
                    {
                        string fullPath = sec.GetAttributeValue("fullPath").ToString();

                        if (!File.Exists(fullPath))
                        {
                            logger.LogInfo(true, "Removed stale fastCgi handler {0}", fullPath);
                            fastCgiHandlers.Remove(sec);
                            changesMade = true;
                        }
                    }
                }

                if (changesMade)
                {
                    UtilsIis.CommitChanges(serverManager);
                }
                else
                {
                    logger.LogInfo(true, "No stale FastCGI configurations found");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void CleanUpStaleApplicationPools(ILoggerInterface logger)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                bool changesMade = false;
                var appPools = serverManager.ApplicationPools;
                var usedAppPools = new HashSet<string>();

                // Get the application pools currently in use by websites
                foreach (var site in serverManager.Sites)
                {
                    foreach (var app in site.Applications)
                    {
                        usedAppPools.Add(app.ApplicationPoolName);
                    }
                }

                // Remove any application pools that are not in use
                foreach (var appPool in appPools.ToList())
                {
                    if (!usedAppPools.Contains(appPool.Name))
                    {
                        logger.LogInfo(true, "Removed stale application pool {0}", appPool.Name);
                        appPools.Remove(appPool);
                        changesMade = true;
                    }
                }

                if (changesMade)
                {
                    serverManager.CommitChanges();
                }
                else
                {
                    logger.LogInfo(true, "No stale application pools found.");
                }
            }
        }
    }
}
