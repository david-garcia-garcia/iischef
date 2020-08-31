using iischef.logger;
using Microsoft.Web.Administration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace iischef.utils
{
    /// <summary>
    /// Random IIS management utilities
    /// </summary>
    public class UtilsIis
    {
        public const string LOCALHOST_ADDRESS = "127.0.0.1";

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
        protected static ConfigurationElement FindElement(
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
            using (RegistryKey componentsKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\InetStp", false))
            {
                if (componentsKey != null)
                {
                    int majorVersion = (int)componentsKey.GetValue("MajorVersion", -1);
                    int minorVersion = (int)componentsKey.GetValue("MinorVersion", -1);

                    if (majorVersion != -1 && minorVersion != -1)
                    {
                        return new Version(majorVersion, minorVersion);
                    }
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
        /// Central store path for certificates. Returns exception if not configured or cannot be returned.
        /// </summary>
        public static string CentralStorePath(ILoggerInterface logger)
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
        /// Set specific account anonymous authentication for an IIS application
        ///
        /// The default behaviour for IIS is to have the ANONYMOUS user (that is
        /// used for all request) identified as IUSR. When using FAST-CGI impersonation,
        /// we WANT all permissions to be based on the application pool identity...
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public static void ConfigureAnonymousAuthForIisApplication(
            string siteName,
            string username,
            string password)
        {
            using (ServerManager serverManager = new ServerManager())
            {
                // fastCgi settings in IIS can only be set at the HOSTS level
                // we found no way to set this at a web.config level.
                Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection section;

                // TODO: The type of authentication and it's configuration should be configurable here...
                // see https://www.iis.net/configreference/system.webserver/security/authentication
                section = config.GetSection("system.webServer/security/authentication/anonymousAuthentication", siteName);

                section["enabled"] = true;
                section["password"] = password;
                section["username"] = username;

                UtilsIis.CommitChanges(serverManager);
            }
        }

        /// <summary>
        /// Wrapper for commit changes in server manager.
        ///
        /// Because there is usually a delay, it tries to stop current execution until
        /// changes have been actually applied.
        /// </summary>
        /// <param name="sm"></param>
        public static void CommitChanges(ServerManager sm)
        {
            sm.CommitChanges();
            Thread.Sleep(250);
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
                },
                (e) => true,
                3000,
                logger);
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

            using (ServerManager serverManager = new ServerManager())
            {
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
                    ConfigurationElement addElement = allowedServerVariablesCollection.CreateElement("add");
                    addElement["name"] = missingVariable;
                    allowedServerVariablesCollection.Add(addElement);
                }

                serverManager.CommitChanges();
            }
        }

        /// <summary>
        /// Find a certificate in IIS central certificate store
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="logger"></param>
        /// <param name="certificatePath"></param>
        /// <returns></returns>
        public static X509Certificate2 FindCertificateInCentralCertificateStore(
            string hostName,
            ILoggerInterface logger,
            out string certificatePath)
        {
            string centralStorePath = UtilsIis.CentralStorePath(logger);
            FileInfo certificateFile = null;

            // Look for a certificate file that includes wildcard matching logic
            // https://serverfault.com/questions/901494/iis-wildcard-https-binding-with-centralized-certificate-store
            var hostNameParts = hostName.Split(".".ToCharArray()).Reverse().ToList();

            foreach (var f in new DirectoryInfo(centralStorePath).EnumerateFiles())
            {
                // Check if this certificate file is valid for the hostname...
                var certNameParts = Path.GetFileNameWithoutExtension(f.FullName).Split(".".ToCharArray()).Reverse()
                    .ToList();

                // This won't allow for nested subdomain with wildcards, but it's a good starting point
                // i.e. a hostname such as "a.mytest.mydomain.com" won't be matched to a certifica
                // such as "_.mydomain.com"
                // but "mytest.mydomain.com" will match to "_.mydomain.com".
                if (certNameParts.Count != hostNameParts.Count)
                {
                    continue;
                }

                bool isMatch = true;

                for (int x = 0; x < hostNameParts.Count; x++)
                {
                    if (hostNameParts[x] == "*" || certNameParts[x] == "_")
                    {
                        continue;
                    }

                    if (hostNameParts[x] != certNameParts[x])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    certificateFile = f;
                    break;
                }
            }

            certificatePath = certificateFile?.FullName;

            // This is null on purpose.
            string certificatePassword = null;

            X509Certificate2Collection collection = new X509Certificate2Collection();

            if (certificateFile != null)
            {
                logger.LogInfo(true, "Found potential certificate matching file at {0}", certificateFile.FullName);

                try
                {
                    // Usamos ephemeral keyset para que no almacene claves en la máquina todo el tiempo...
                    collection.Import(certificateFile.FullName, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
                    var originalCert = collection[0];

                    logger.LogInfo(true, "Certificate IssuerName '{0}'", originalCert.IssuerName.Name);
                    logger.LogInfo(true, "Certificate FriendlyName '{0}'", originalCert.FriendlyName);
                    logger.LogInfo(true, "Certificate SubjectName '{0}'", originalCert.SubjectName.Name);
                    logger.LogInfo(true, "Certificate NotBefore '{0}'", originalCert.NotBefore.ToString("HH:mm:ss yyyy/MM/dd"));

                    return originalCert;
                }
                catch (Exception e)
                {
                    logger.LogWarning(false, $"Error importing certificate: '{certificateFile.FullName}'." + e.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// IIS is very bad at detecting and handling changes in certificates stored in the
        /// central certificate store, use this method to ensure that a hostname bound
        /// to a SSL termination is properly updated throughout IIS
        ///
        /// https://docs.microsoft.com/en-us/iis/get-started/whats-new-in-iis-85/certificate-rebind-in-iis85
        /// https://delpierosysadmin.wordpress.com/2015/02/23/iis-8-5-enable-automatic-rebind-of-renewed-certificate-via-command-line/
        /// </summary>
        public static void EnsureCertificateInCentralCertificateStoreIsRebound(string hostname, ILoggerInterface logger)
        {
            Dictionary<string, List<Binding>> temporaryBindings = new Dictionary<string, List<Binding>>();

            using (var sm = new ServerManager())
            {
                // Al sites that have an SSL termination bound to this hostname
                var sites = UtilsSystem.QueryEnumerable(
                    sm.Sites,
                    (s) => s.Bindings.Any(i => i.Protocol == "https" && hostname.Equals(i.Host, StringComparison.CurrentCultureIgnoreCase)),
                    (s) => s,
                    (s) => s.Name,
                    logger).ToList();

                // Remove temporarily
                foreach (var site in sites)
                {
                    foreach (var binding in site.Bindings.Where((i) => i.Protocol == "https" && hostname.Equals(i.Host, StringComparison.CurrentCultureIgnoreCase)).ToList())
                    {
                        if (!temporaryBindings.ContainsKey(site.Name))
                        {
                            temporaryBindings[site.Name] = new List<Binding>();
                        }

                        logger.LogInfo(true, "Removed binding {0} from site {1}", binding.BindingInformation, site.Name);

                        temporaryBindings[site.Name].Add(binding);
                        site.Bindings.Remove(binding);
                    }
                }

                CommitChanges(sm);
            }

            // This wait here helps...
            Thread.Sleep(2000);

            // Now restore...
            using (var sm = new ServerManager())
            {
                foreach (var siteName in temporaryBindings.Keys)
                {
                    var site = FindSiteWithName(sm, siteName, logger).Single();

                    foreach (var binding in temporaryBindings[siteName])
                    {
                        var b = site.Bindings.Add(binding.BindingInformation, binding.Protocol);
                        b.SslFlags = binding.SslFlags;
                        b.CertificateStoreName = binding.CertificateStoreName;
                        b.UseDsMapper = binding.UseDsMapper;

                        logger.LogInfo(true, "Restored binding {0} to site {1}", binding.BindingInformation, site.Name);
                    }
                }

                CommitChanges(sm);
            }

            // This wait here helps also...
            Thread.Sleep(2000);
        }
    }
}
