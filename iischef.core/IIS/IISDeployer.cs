using iischef.core.Configuration;
using iischef.core.Storage;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace iischef.core.IIS
{
    /// <summary>
    /// Deployer for IIS site
    /// </summary>
    public class IISDeployer : DeployerBase, IDeployerInterface
    {
        /// <summary>
        /// The settings for this deployer
        /// </summary>
        protected IISSettings IisSettings;

        /// <summary>
        /// We had some bugs that ended up in the same site
        /// redeployed hundreds of times collapsing the servers.
        /// 
        /// This is just a security measure in case future bug
        /// has similar fatal result.
        /// </summary>
        public const int CST_MAX_IIS_SITES = 350;

        /// <summary>
        /// PATH for the settings of this deployer
        /// </summary>
        public const string CST_SETTINGS_WEBROOT = "iis.runtime.webroot.path";

        /// <summary>
        /// Service to provision SSL certificates
        /// </summary>
        protected SslCertificateProviderService SslProvisioningManager;

        /// <summary>
        /// Application pool utils
        /// </summary>
        protected UtilsAppPool AppPoolUtils;

        /// <summary>
        /// 
        /// </summary>
        protected UtilsHosts UtilsHosts;

        /// <summary>
        /// Helper to setup the shared cdn proxy
        /// </summary>
        protected CdnHelper CdnHelper;

        /// <inheritdoc cref="DeployerBase"/>
        public override void initialize(
            EnvironmentSettings globalSettings,
            JObject deployerSettings,
            Deployment deployment,
            ILoggerInterface logger,
            InstalledApplication inhertApp)
        {
            base.initialize(globalSettings, deployerSettings, deployment, logger, inhertApp);

            this.IisSettings = deployerSettings.castTo<IISSettings>();
            this.IisSettings.InitializeDefaults();

            this.AppPoolUtils = new UtilsAppPool(logger);
            this.UtilsHosts = new UtilsHosts(logger);
            this.CdnHelper = new CdnHelper(logger, globalSettings);
        }

        /// <summary>
        /// Default application pool is needed during temporary site setup
        /// </summary>
        protected void EnsureDefaultApplicationPoolExists()
        {
            using (ServerManager manager = new ServerManager())
            {
                var defaultPool = (from p in manager.ApplicationPools
                                   where p.Name == "DefaultAppPool"
                                   select p).FirstOrDefault();

                if (defaultPool != null)
                {
                    return;
                }

                this.Logger.LogWarning(false, "No DefaultAppPool found, creating one now.");

                var pool = manager.ApplicationPools.Add("DefaultAppPool");
                UtilsIis.CommitChanges(manager);
            }
        }

        /// <summary>
        /// Make sure that we have not reached the limit
        /// of sites deployed in IIS
        /// </summary>
        protected void EnsureIisHasSpaceAndSiteNotRepeated()
        {
            using (ServerManager manager = new ServerManager())
            {
                var siteCount = manager.Sites.Count;

                if (siteCount > CST_MAX_IIS_SITES)
                {
                    throw new Exception(
                        $"Maximum number of IIS sites ({siteCount}) reached. Cannot deploy new application.");
                }

                this.Logger.LogInfo(true, $"Currently deployed {siteCount} of {CST_MAX_IIS_SITES} maximum iis websites.");

                // Check that this site is not deployed more than N times (something is wrong!!!)
                int limit = 3;

                var matches = (from p in manager.Sites
                               where this.Deployment.IsShortId(p.Name)
                               select p);

                if (matches.Count() > limit)
                {
                    throw new Exception("Cannot redeploy site, other sites are using the same site pattern (possible bug), please cleanup stuck sites with prefix: " + this.Deployment.GetShortIdPrefix());
                }
            }
        }

        /// <summary>
        /// Not the best place but... make sure we have a daily
        /// backup of ApplicationHosts.config for the last 10 days.
        /// 
        /// This is not part of the deployment process or any resiliency
        /// meassure... it's just here to manually troubleshot iis in case
        /// of failure.
        /// </summary>
        protected void BackupApplicationHosts()
        {
            IntPtr wow64Value = IntPtr.Zero;

            UtilsIis.Wow64DisableWow64FsRedirection(ref wow64Value);

            try
            {
                var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%windir%\System32\inetsrv\config\applicationHost.config"));

                // Today's zipped copy.
                var todaysCopy = Path.GetFullPath(Environment.ExpandEnvironmentVariables(
                    $@"%windir%\System32\inetsrv\config\{DateTime.Now:yyyyMMdd}_applicationHost.bak"));

                if (!File.Exists(todaysCopy))
                {
                    File.Copy(path, todaysCopy);
                }

                // Remove all backups that are more than 30 days old...
                var dir = Path.GetDirectoryName(path);
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    var fifo = new FileInfo(f);

                    if (fifo.Extension == ".bak" && fifo.Name.Contains("_applicationHost") && (DateTime.UtcNow - fifo.LastWriteTimeUtc).TotalDays > 10)
                    {
                        fifo.Delete();
                    }
                }
            }
            catch (Exception e)
            {
                this.Logger.LogException(e);
            }
            finally
            {
                UtilsIis.Wow64RevertWow64FsRedirection(wow64Value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected void EnsureSslCertificateProviderInitialized()
        {
            if (this.SslProvisioningManager != null)
            {
                return;
            }

            this.SslProvisioningManager = new SslCertificateProviderService(this.Logger, this.Deployment.installedApplicationSettings.GetId(), this.GlobalSettings, this.Deployment); // The application Id
        }

        /// <summary>
        /// The main site's name
        /// </summary>
        /// <returns></returns>
        protected string GetSiteName(Deployment d)
        {
            return d.getShortId();
        }

        /// <summary>
        /// Get the offline site's name
        /// </summary>
        /// <returns></returns>
        protected string GetOfflineSiteName(Deployment d)
        {
            return "off_" + d.getShortId();
        }

        /// <summary>
        /// The internal hostname asigned to all websites, shared accross deployments.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        protected string GetInternalHostname(Deployment d)
        {
            return UtilsIis.ValidateHostName("localsite." + d.installedApplicationSettings.GetId());
        }

        /// <summary>
        /// 
        /// </summary>
        public void deploy()
        {
            this.Logger.LogInfo(true, "IIS Version {0}", UtilsIis.GetIisVersion());

            this.EnsureSslCertificateProviderInitialized();
            this.EnsureIisHasSpaceAndSiteNotRepeated();
            this.EnsureDefaultApplicationPoolExists();
            this.BackupApplicationHosts();

            var limits = this.Deployment.GetApplicationLimits();

            // VER ESTO PARA REFERENCIAS SOBRE COMO MANIPULAR CONFIGURACIONES DEL POOL Y SITIOS
            // http://stackoverflow.com/questions/27116530/how-to-programmatically-set-app-pool-identity

            // Only one site per deployment...
            var siteName = this.GetSiteName(this.Deployment);

            // Use an empty temporary folder to mount the site on
            var tempDir = UtilsSystem.EnsureDirectoryExists(Path.Combine(this.Deployment.appPath, Guid.NewGuid().ToString()), true);

            using (ServerManager manager = new ServerManager())
            {
                // Deploy the site, ideally NO site should be found
                // here as it indicates a failed previous deployment.
                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).SingleOrDefault();

                if (site != null)
                {
                    throw new Exception($"A site already exists with name '{siteName}''");
                }

                // Deploy the application pools
                foreach (var p in this.IisSettings.pools)
                {
                    this.DeployApplicationPool(manager, p.Value, limits);
                }

                this.Logger.LogInfo(true, "IIS Deployer: adding temporary site: {0}", siteName);

                // Create and reset a new site on a phantom directory
                // only for web.config visibility purposes.
                site = manager.Sites.Add(siteName, tempDir, 80);

                // We need to add a valid binding to the site, otherwise it gets corrupted.
                // We assign an internal hostname to all sites.
                site.Bindings.Remove(site.Bindings.First());
                site.Bindings.Add($"127.0.0.1:80:{this.GetInternalHostname(this.Deployment)}", "http");
                this.UtilsHosts.AddHostsMapping("127.0.0.1", this.GetInternalHostname(this.Deployment), this.Deployment.getShortId());

                // Commit the changes
                UtilsIis.CommitChanges(manager);
            }

            UtilsIis.WaitForSiteToBeAvailable(siteName, this.Logger);
            this.AppPoolUtils.WebsiteAction(siteName, AppPoolActionType.Stop, skipApplicationPools: true);
            UtilsIis.ConfigureAnonymousAuthForIisApplication(siteName, this.Deployment.WindowsUsernameFqdn(), this.Deployment.GetWindowsPassword());

            // Configure the temporary directory for the website
            using (ServerManager manager = new ServerManager())
            {
                // Deploy the site, ideally NO site should be found
                // here as it indicates a failed previous deployment.
                // Query the sites in a resilient way...
                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).Single();

                // If stores in a NAS, IIS logs cannot collide!
                string iisLogDir =
                    UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.Deployment.logPath, "IISLogs", Environment.MachineName), true);

                // Populate specific log folder for site, see
                // https://superuser.com/questions/625975/iis-log-folder-permissions-not-being-inherited
                string iisSiteDir =
                    UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(iisLogDir, $"W3SVC{site.Id}"), true);

                // In AD scenario, the destination folder needs permission of local machine as that is the identity
                // that IIS will run under when writting the logs, not the application identity.
                if (this.GlobalSettings.directoryPrincipal?.ContextType == nameof(ContextType.Domain))
                {
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing($"{Environment.MachineName}$", iisSiteDir, FileSystemRights.FullControl, this.GlobalSettings.directoryPrincipal);
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing($"{Environment.MachineName}$", iisLogDir, FileSystemRights.FullControl, this.GlobalSettings.directoryPrincipal);
                }

                // We also explictly give the application's user permission, so that AddPermissionToDirectoryIfMissing enables permission inheritance
                UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), iisSiteDir, FileSystemRights.Write, this.GlobalSettings.directoryPrincipal);

                site.LogFile.SetAttributeValue("Directory", iisLogDir);

                UtilsIis.CommitChanges(manager);
            }

            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).Single();

                // IMPORTANT NOTE: At some point we tried to have environment variable propagated
                // by nesting the actual root directory in a virutal directory itself and using
                // a rewrite rule in the root (see IIS/web.config). This turned out to BREAK many
                // existing applications. I.E. The rewrite module will only work once per request,
                // so any rewrite rule hosted in the virtual directory will not work. This break
                // for example DRUPAL that relies on url rewriting...
                var rootMount = this.IisSettings.getRootMount();
                var rootPool = this.IisSettings.getPoolForBinding(rootMount.pool);

                var rootApp = site.Applications.Single();

                // Set the proper root
                var webRootPath = UtilsSystem.CombinePaths(this.Deployment.appPath, this.IisSettings.getRootMount().path);
                this.Deployment.SetSetting(CST_SETTINGS_WEBROOT, webRootPath);
                rootApp.VirtualDirectories.First().PhysicalPath = webRootPath;

                // Wether we have or not SSL bindings, mount the .well-known shared directory
                var wellKnowDestinationPath = Path.Combine(webRootPath, ".well-known");
                if (!UtilsJunction.IsJunctionOrSymlink(wellKnowDestinationPath) && Directory.Exists(wellKnowDestinationPath))
                {
                    UtilsSystem.DeleteDirectory(wellKnowDestinationPath, this.Logger);
                }

                UtilsJunction.EnsureLink(wellKnowDestinationPath, this.SslProvisioningManager.GetWellKnownSharedPathForApplication(), this.Logger, false, overWrite: true);

                // Applications pools must match because the request will actually
                // be processed by the origin application pool...
                rootApp.ApplicationPoolName = this.GetPoolIdForDeployment(rootPool);

                this.Logger.LogInfo(true, "Deployed IIS root application with path '{0}' and pool '{1}' for site '{2}' ({3}).", rootApp.Path, rootApp.ApplicationPoolName, site.Name, site.Id);

                // Deploy the remaining mounts
                var mounts = this.IisSettings.mounts.Where((i) => i.Value.root == false).ToList();

                foreach (var m in mounts)
                {
                    // Intentar primero con el path absoluto, si no existe tratarlo como relativo
                    // a la raíz del arterfacto.
                    var physicalpath = this.Deployment.ExpandPaths(m.Value.path);

                    // We can point to runtime paths that do not exist yet.
                    bool isRuntimePath = physicalpath.StartsWith(this.Deployment.runtimePath);

                    if (!Directory.Exists(physicalpath) && !isRuntimePath)
                    {
                        physicalpath = UtilsSystem.CombinePaths(this.Deployment.appPath, m.Value.path);
                    }

                    // Normalize the physical paths to windows directory separator
                    physicalpath = physicalpath.Replace("/", "\\");

                    // Don't '
                    if (!Directory.Exists(physicalpath) && !isRuntimePath)
                    {
                        throw new Exception("Path for IIS mount does not exist: " + physicalpath);
                    }

                    if (!string.IsNullOrWhiteSpace(m.Value.mountpath))
                    {
                        if (!m.Value.mountpath.StartsWith("/"))
                        {
                            throw new Exception(
                                $"Invalid mount path '{m.Value.mountpath}'. IIS mount points must start with a slash.");
                        }
                    }

                    if (m.Value.isVirtualDirectory)
                    {
                        // Explode the path
                        var parts = Regex.Split(m.Value.mountpath.Replace("\\", "/"), "/").Where((i) => !string.IsNullOrWhiteSpace(i)).ToList();
                        var prefix = "/";

                        if (parts.Count > 1)
                        {
                            prefix = "/" + string.Join("/", parts.Take(parts.Count - 1));
                        }

                        var ownerApp = site.Applications.SingleOrDefault(i => i.Path == prefix);

                        if (ownerApp == null)
                        {
                            throw new Exception($"Could not find owner for virtual directory '{m.Value.mountpath}'");
                        }

                        ownerApp.VirtualDirectories.Add("/" + parts.Last(), physicalpath);
                    }
                    else
                    {
                        var app = site.Applications.Add(m.Value.mountpath, physicalpath);
                        var pool = this.IisSettings.getPoolForBinding(m.Value.pool);
                        app.ApplicationPoolName = this.GetPoolIdForDeployment(pool);

                        this.Logger.LogInfo(true, "Deployed IIS application with path '{0}' and pool '{1}' for site '{2}' ({3}).", app.Path, app.ApplicationPoolName, site.Name, site.Id);

                        // preloadEnabled is only available on newer versions of IIS.
                        if (UtilsIis.GetIisVersion() >= Version.Parse("8.0"))
                        {
                            bool preloadEnabled = m.Value.preloadEnabled;

                            if (preloadEnabled && limits.IisVirtualDirectoryAllowPreloadEnabled == false)
                            {
                                this.Logger.LogWarning(true, "Requested preloadEnabled disabled on site due to Chef Application Limits configuration");
                                preloadEnabled = false;
                            }

                            app.SetAttributeValue("preloadEnabled", preloadEnabled);
                        }
                    }
                }

                // Currently we have a stopped site... nothing else...
                UtilsIis.CommitChanges(manager);

                foreach (var m in mounts)
                {
                    string basekey = $"deployers.{this.IisSettings.id}.mounts.{m.Value.id}";
                    this.Deployment.SetRuntimeSetting(basekey + ".mountPath", m.Value.mountpath);
                }
            }

            // Allowed server variables for this application
            UtilsIis.AddAllowedServerVariablesForUrlRewrite(siteName, this.IisSettings.allowedServerVariables.AsIterable().ToArray());

            // Add ip restrictions
            this.ApplyDynamicIpRestrictions(this.IisSettings.ipRestrictions, siteName);

            // Configure machine keys
            this.ConfigureMachineKeys();

            // Prepare all SSL certificates
            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).Single();

                foreach (var b in this.IisSettings.bindings.Where((i) => i.Value.IsSsl()))
                {
                    // Deploy using PrepareSsl, that is, no binding will be added to the site, only
                    // certificate will be checked and provisiones if needed.
                    var deploymentFlags = BindingDeploymentMode.PrepareSsl;

                    if (this.Deployment.GetPreviousDeployment() == null)
                    {
                        // If there is no previous deployment (first one) then fake
                        // the SSL certificates if there are no existing certificates
                        deploymentFlags = deploymentFlags | BindingDeploymentMode.UseSelfSigned;
                    }

                    this.DeployBinding(site, b.Value, "root", true, deploymentFlags);
                }

                // Do not commit this server manager, because during SSL prepare:
                // (A) No real bindings are actually added to the site object
                // (B) It uses a new ServerManager() to provision internally the sites needed to validate the SSL challenges
                // UtilsIis.CommitChanges(manager);
            }

            // Deploy all bindings now
            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).Single();

                foreach (var b in this.IisSettings.bindings)
                {
                    this.DeployBinding(site, b.Value, "root", true);
                }

                UtilsIis.CommitChanges(manager);
            }

            // TODO: We currently merge the site specific CDN settings for backwards compatibilty, but
            // this will be removed in the future and only support the environment specific CDN settings.
            List<CdnBinding> cdnBindings = new List<CdnBinding>();
            List<CdnMount> cdnMounts = new List<CdnMount>();

            this.AddLegacyIisMountsAndBindings(cdnBindings, cdnMounts);

            if (this.GlobalSettings.cdn_bindings?.Any() == true)
            {
                cdnBindings.AddRange(this.GlobalSettings.cdn_bindings);
            }

            var canonicalMount = new CdnMount();
            canonicalMount.destination = this.GetInternalHostname(this.Deployment);
            canonicalMount.id = "chef_canonical";
            canonicalMount.match = $"cdn_{this.Deployment.installedApplicationSettings.GetId()}/";

            // Make the canonical path the first one, to make sure this one is used as the preferred CDN prefix
            cdnMounts.Insert(0, canonicalMount);

            this.DeployCdn(cdnBindings, cdnMounts, siteName);

            UtilsSystem.DeleteDirectory(tempDir, this.Logger, 6);

            this.CheckSitesWithoutSni();
        }

        [Obsolete("To be removed in the future. Only server specific cdn settings will be deployed.")]
        protected void AddLegacyIisMountsAndBindings(List<CdnBinding> cdnBindings, List<CdnMount> cdnMounts)
        {
            if (this.IisSettings.cdn_bindings != null)
            {
                var cdnBinding = new CdnBinding();
                cdnBinding.id = "legacy";
                cdnBinding.OriginBindings = new List<Binding>();
                cdnBinding.OriginBindings.AddRange(this.IisSettings.cdn_bindings.Values);
                cdnBindings.Add(cdnBinding);
            }

            if (this.IisSettings.cdn_mounts != null)
            {
                cdnMounts.AddRange(this.IisSettings.cdn_mounts.Values);
            }
        }

        /// <summary>
        /// Setup dynamic ip restrictions
        /// </summary>
        /// <param name="restrictions"></param>
        /// <param name="siteName"></param>
        protected void ApplyDynamicIpRestrictions(IISSettingsIpRestrictions restrictions, string siteName)
        {
            if (restrictions?.enabled != true)
            {
                return;
            }

            using (ServerManager serverManager = new ServerManager())
            {
                Microsoft.Web.Administration.Configuration config = serverManager.GetApplicationHostConfiguration();

                ConfigurationSection dynamicIpSecuritySection = config.GetSection("system.webServer/security/dynamicIpSecurity", siteName);

                List<string> validDeniedActions = new List<string>() { "Forbidden", "AbortRequest", "Unauthorized", "NotFound" };

                string denyAction = restrictions.denyAction ?? "Forbidden";
                if (!validDeniedActions.Contains(denyAction))
                {
                    throw new Exception($"Invalid deny action {denyAction} must be one of {string.Join(",", validDeniedActions)}");
                }

                dynamicIpSecuritySection["denyAction"] = denyAction;
                dynamicIpSecuritySection["enableLoggingOnlyMode"] = restrictions.enableLoggingOnlyMode;
                dynamicIpSecuritySection["enableProxyMode"] = restrictions.enableProxyMode;

                ConfigurationElement denyByConcurrentRequestsElement = dynamicIpSecuritySection.GetChildElement("denyByConcurrentRequests");
                denyByConcurrentRequestsElement["enabled"] = restrictions.denyByConcurrentRequests_enabled;
                denyByConcurrentRequestsElement["maxConcurrentRequests"] = restrictions.denyByConcurrentRequests_maxConcurrentRequests;

                ConfigurationElement denyByRequestRateElement = dynamicIpSecuritySection.GetChildElement("denyByRequestRate");
                denyByRequestRateElement["enabled"] = restrictions.denyByRequestRate_enabled;
                denyByRequestRateElement["maxRequests"] = restrictions.denyByRequestRate_maxRequests;
                denyByRequestRateElement["requestIntervalInMilliseconds"] = restrictions.denyByRequestRate_requestIntervalInMilliseconds;

                ConfigurationSection ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity", siteName);
                ipSecuritySection["enableProxyMode"] = restrictions.ipSecurity_enableProxyMode;
                ipSecuritySection["enableReverseDns"] = restrictions.ipSecurity_enableReverseDns;

                ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();

                var addresses = restrictions.ipSecurity_addresses ?? new List<IISSettingsIpRestrictions.IpEntry>();

                foreach (var address in this.GlobalSettings.safeIpAddresses.AsIterable())
                {
                    addresses.Add(new IISSettingsIpRestrictions.IpEntry()
                    {
                        ipAddress = address,
                        allowed = true
                    });
                }

                List<string> processedIpAddresses = new List<string>();

                foreach (var ip in addresses)
                {
                    if (processedIpAddresses.Contains(ip.ipAddress))
                    {
                        continue;
                    }

                    processedIpAddresses.Add(ip.ipAddress);

                    ConfigurationElement addElement = ipSecurityCollection.CreateElement("add");

                    var parsed = UtilsIis.GetSubnetAndMaskFromCidr(ip.ipAddress);

                    addElement["ipAddress"] = parsed.Item1.ToString();

                    if (!string.IsNullOrWhiteSpace(ip.domainName))
                    {
                        addElement["domainName"] = ip.domainName;
                    }

                    if (parsed.Item2 != null)
                    {
                        addElement["subnetMask"] = parsed.Item2.ToString();
                    }

                    addElement["allowed"] = ip.allowed;

                    ipSecurityCollection.Add(addElement);
                }

                UtilsIis.CommitChanges(serverManager);
            }
        }

        // This is what a directory based rewrite rule looks like....
        //
        // <configuration>
        //   <system.webServer>
        //     <rewrite>
        //       <rules>
        //                 <rule name = "ReverseProxyInboundRule1" stopProcessing="true">
        //                     <match url = "^php_sabentisplus" />
        //                     < action type="Rewrite" url="http://local.sabentisplus.com/{C:2}/" />
        //                     <conditions>
        //                         <add input = "{PATH_INFO}" pattern="(^/php_sabentisplus)(.*)" />
        //                     </conditions>
        //                 </rule>
        //       </rules>
        //     </rewrite>
        //   </system.webServer>
        // </configuration>

        /// <summary>
        /// CDN is a shared domain/dns binding accross ALL sites on a server
        /// so that they can be accesed on a sub-url such as sharecdn.com/mysite
        /// this is done like that because most CDN providers charge per-pull-zone
        /// binded to single source domain. In this way we can share many sites
        /// on a single pull-zone.
        /// </summary>
        /// <param name="cdnBindings">The bindings for the CDN site</param>
        /// <param name="cdnMounts">The "mounts" AKA rewrite urls</param>
        /// <param name="siteName">The owner site</param>
        protected void DeployCdn(List<CdnBinding> cdnBindings, List<CdnMount> cdnMounts, string siteName)
        {
            // If there are no mounts or no bindings, then do nothing
            if (cdnMounts?.Any() != true || cdnBindings?.Any() != true)
            {
                return;
            }

            this.CdnHelper.ConfigureProxy();
            this.CdnHelper.GetCdnWebConfigPathInitialized();
            this.CdnHelper.AddCacheBusterRewriteRule();

            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, this.CdnHelper.CstChefCndSiteName, this.Logger).FirstOrDefault();

                if (site == null)
                {
                    throw new Exception("Cannot find CDN site with name: " + this.CdnHelper.CstChefCndSiteName);
                }

                this.UtilsHosts.AddHostsMapping(UtilsIis.LOCALHOST_ADDRESS, this.CdnHelper.CstChefInternalHostname, "chf_IISDeployer_CDN");

                foreach (var cdnBinding in cdnBindings)
                {
                    foreach (var binding in cdnBinding.OriginBindings.AsIterable())
                    {
                        if (string.IsNullOrWhiteSpace(binding.id))
                        {
                            throw new Exception("All CDN bindings must have an ID.");
                        }

                        // Sites do not actually own the CDN binding, so do not add them to hosts
                        binding.addtohosts = false;

                        this.DeployBinding(site, binding, "cdn", true);
                    }
                }

                // This includes all effective bindings for the site (excluding ssl bindings that are setup after deployment)
                // This is OK because we don't want to handle internal SSL loops
                List<string> availableBindings =
                    UtilsIis.FindSiteWithName(manager, siteName, this.Logger)
                    .Single()
                    .Bindings.Select((i) => i.Host)
                    .ToList();

                // Validate that all mounts point to a valid internal hostname
                foreach (var cdnmount in cdnMounts)
                {
                    // Grab the destionation's hostname
                    var destionationHostName = Regex.Split(cdnmount.destination, "/").First();

                    var existingBinding = availableBindings.FirstOrDefault(hostname => destionationHostName.Equals(hostname, StringComparison.CurrentCultureIgnoreCase));

                    if (existingBinding == null)
                    {
                        string availableBindingsString = string.Join(", ", availableBindings);

                        throw new Exception(
                            $"cdn_mount '{cdnmount.id}' has destination '{cdnmount.destination}' who's hostname does not exist in the application bindings: '{availableBindingsString}'");
                    }
                }

                UtilsIis.CommitChanges(manager);
            }

            // Deploy
            foreach (var mount in cdnMounts)
            {
                string mountname = this.Deployment.getShortId() + "_" + mount.id;

                if (string.IsNullOrEmpty(mount.destination))
                {
                    throw new Exception("CDN mount destination cannot be empty.");
                }

                if (string.IsNullOrEmpty(mount.id))
                {
                    throw new Exception("CDN mount id cannot be empty.");
                }

                if (string.IsNullOrEmpty(mount.match))
                {
                    throw new Exception("CDN mount match cannot be empty.");
                }

                var match = $"^{Regex.Escape(mount.match)}(.*)";

                // Se list of avaiable server variables here: https://docs.microsoft.com/en-us/iis/web-dev-reference/server-variables
                string value = @"
                <rule name=""{2}"" stopProcessing=""true"" >
                    <match url = ""{0}"" />
                    <action type = ""Rewrite"" url=""http://{1}/{{R:1}}"" />
                    <serverVariables>
                        <set name=""HTTP_X_FORWARDED_FOR"" value=""{{REMOTE_ADDR}}"" replace=""false"" />
                        <set name=""HTTP_X_FORWARDED_HOST"" value=""{{SERVER_NAME}}"" replace=""false"" />
                        <set name=""HTTP_X_FORWARDED_PROTO"" value=""{{C:1}}"" replace=""false"" />
                    </serverVariables>
                    <conditions>
                        <add input=""{{CACHE_URL}}"" pattern=""^(.*)://"" />
                    </conditions>
                </rule>
                        ";
                var template = string.Format(value, SecurityElement.Escape(match), SecurityElement.Escape(mount.destination), SecurityElement.Escape(mountname));
                this.CdnHelper.AddRewriteRule(template);
            }

            // Generate CDN information to be consumed by the application

            string preferredCdnPrefix = null;

            foreach (var mount in cdnMounts)
            {
                foreach (var cdnBinding in cdnBindings)
                {
                    foreach (var binding in cdnBinding.OriginBindings.AsIterable())
                    {
                        this.Deployment.SetRuntimeSetting($"cdn.{cdnBinding.id}.origins.{binding.id}_{mount.id}.uri", binding.GetScheme() + "://" + binding.hostname + "/" + mount.match);
                        this.Deployment.SetRuntimeSetting($"cdn.{cdnBinding.id}.origins.{binding.id}_{mount.id}.hostname", binding.hostname);
                    }

                    int x = 0;
                    foreach (var edgeUrl in cdnBinding.EdgeUrls.AsIterable())
                    {
                        if (!Uri.TryCreate(edgeUrl, UriKind.Absolute, out var edgeUri))
                        {
                            throw new Exception($"Invalid edge URL: '{edgeUrl}'. Edge URI must be an absolute URL with protocol.");
                        }

                        if (preferredCdnPrefix.IsNullOrDefault() && edgeUri.Scheme.ToLower() == "https")
                        {
                            preferredCdnPrefix = edgeUri.ToString().TrimEnd("/".ToCharArray()) + "/" + mount.match;
                        }

                        this.Deployment.SetRuntimeSetting($"cdn.{cdnBinding.id}.edges.{x}.url", edgeUrl);
                        x++;
                    }
                }
            }

            this.Deployment.SetRuntimeSetting($"cdn.preferred_prefix", preferredCdnPrefix);
        }

        /// <summary>
        /// Remove the CDN bindings
        /// </summary>
        protected void RemoveCdnRules()
        {
            // All cdn rules have this deployment short id prefix
            string cdnRuleNamePrefix = this.Deployment.getShortId() + "_";
            this.CdnHelper.RemoveRewriteRulesWithPrefix(cdnRuleNamePrefix, this.Logger);

            // Remove legacy rules that where constructed with old naming scheme

            foreach (var mount in this.IisSettings.cdn_mounts.AsIterable())
            {
                string mountname = this.Deployment.installedApplicationSettings.GetId() + "_" + mount.id;
                this.CdnHelper.RemoveRewriteRulesWithPrefix(mountname, this.Logger);
            }
        }

        /// <summary>
        /// Generate a persistent machine key for the application if requested
        /// </summary>
        protected void ConfigureMachineKeys()
        {
            string siteName = this.GetSiteName(this.Deployment);

            using (var serverManager = new ServerManager())
            {
                bool configModified = false;

                Site site = (from p in serverManager.Sites
                             where p.Name == siteName
                             select p).Single();

                foreach (var app in site.Applications)
                {
                    var mount = this.IisSettings.mounts.SingleOrDefault(i => i.Value.mountpath == app.Path || (i.Value.root && app.Path == "/"));

                    if (string.IsNullOrWhiteSpace(mount.Value.machineKeyDeriveFrom))
                    {
                        continue;
                    }

                    var pool = serverManager.ApplicationPools.Single((i) =>
                        i.Name.Equals(app.ApplicationPoolName, StringComparison.CurrentCultureIgnoreCase));

                    if (string.IsNullOrWhiteSpace(pool.ManagedRuntimeVersion))
                    {
                        continue;
                    }

                    string deriveFrom = mount.Value.machineKeyDeriveFrom;

                    if (deriveFrom.Equals("auto", StringComparison.CurrentCultureIgnoreCase))
                    {
                        deriveFrom = this.Deployment.installedApplicationSettings.GetId();
                    }

                    string key64Byte = this.GenerateKey(64, "validationKey" + deriveFrom);
                    string key32Byte = this.GenerateKey(32, "decryptionKey" + deriveFrom);

                    try
                    {
                        Microsoft.Web.Administration.Configuration config = serverManager.GetWebConfiguration(siteName, app.Path);
                        var section = config.GetSection("system.web/machineKey");
                        section.SetAttributeValue("validationKey", key64Byte);
                        section.SetAttributeValue("decryptionKey", key32Byte);
                        section.SetAttributeValue("validation", "SHA1");
                        section.SetAttributeValue("decryption", "AES");
                        configModified = true;
                    }
                    catch (FileNotFoundException)
                    {
                        // Do nothing, the application might not have a web.config file
                    }
                }

                if (configModified)
                {
                    serverManager.CommitChanges();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytelength"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public string GenerateKey(int bytelength, string from)
        {
            var fromHashBytes = Encoding.UTF8.GetBytes(UtilsEncryption.GetSHA256(from));
            StringBuilder hexString = new StringBuilder(64);

            for (int counter = 0; counter < bytelength; counter++)
            {
                hexString.Append($"{fromHashBytes[counter]:X2}");
            }

            return hexString.ToString();
        }

        /// <summary>
        /// Configure the .Net framework temp directory, which should live with the site deployment
        /// exclusively.
        ///
        /// Unfortunately this was the ONLY way to do this (modifying the actual application's web.config)
        /// because this setting cannot be declared at an applicationHosts level.
        /// </summary>
        /// <param name="siteName"></param>
        protected void ConfigureNetFrameworkTempFolderForPath(
            string siteName)
        {
            using (var serverManager = new ServerManager())
            {
                Site site = (from p in serverManager.Sites
                             where p.Name == siteName
                             select p).Single();

                foreach (var app in site.Applications)
                {
                    Microsoft.Web.Administration.Configuration config = serverManager.GetWebConfiguration(siteName, app.Path);

                    try
                    {
                        var section = config.GetSection("system.web/compilation");

                        var path = UtilsSystem.EnsureDirectoryExists(
                            Path.Combine(this.Deployment.runtimePathWritable, "NetFrameworkTemporaryFiles"), true);

                        section.SetAttributeValue("tempDirectory", path);
                    }
                    catch (FileNotFoundException)
                    {
                        // Do nothing, the application might not have a web.config file
                    }
                }

                serverManager.CommitChanges();
            }
        }

        /// <summary>
        /// During hot switch deployments we need to stop previous
        /// site prior to undeploying...
        /// </summary>
        public void stop()
        {
            // Asssume no site here... nothing to stop.
            if (this.Deployment == null)
            {
                return;
            }

            this.AppPoolUtils.WebsiteAction(this.GetSiteName(this.Deployment), AppPoolActionType.Stop);
            this.AppPoolUtils.WebsiteAction(this.GetOfflineSiteName(this.Deployment), AppPoolActionType.Start);
        }

        /// <summary>
        /// Executed after previous deployment is stopped.
        ///
        /// Bindings have exclusive in a single IIS instalation so
        /// it is very important that previous deployment is stopped
        /// before we try to deploy the bindings.
        /// </summary>
        public override void beforeDone()
        {
            var siteName = this.GetSiteName(this.Deployment);

            // Configure the NetFramework tempo folder after deployment has been through, because
            // the contents might not yet exist when deployer is run.
            var deploymentStrategy = this.Deployment.installedApplicationSettings.GetApplicationMountStrategy();

            if (deploymentStrategy == ApplicationMountStrategy.Move || deploymentStrategy == ApplicationMountStrategy.Copy)
            {
                this.ConfigureNetFrameworkTempFolderForPath(siteName);
            }
        }

        public override void done()
        {
            this.SetupOfflineSite();
        }

        protected void SetupOfflineSite()
        {
            using (ServerManager manager = new ServerManager())
            {
                var offlineSiteName = this.GetOfflineSiteName(this.Deployment);
                var existingSite = UtilsIis.FindSiteWithName(manager, offlineSiteName, this.Logger).FirstOrDefault();

                if (existingSite != null)
                {
                    throw new Exception("Offline site already exists?");
                }

                var indexFile = Path.Combine(this.Deployment.runtimePath, "site_offline", "index.html");
                var webConfigFile = Path.Combine(this.Deployment.runtimePath, "site_offline", "web.config");
                var rootDir = Path.GetDirectoryName(indexFile);

                UtilsSystem.EnsureDirectoryExists(rootDir, true);

                // Give permission in this directory to DefaultAppPool
                UtilsWindowsAccounts.AddEveryonePermissionToDir(rootDir, UtilsWindowsAccounts.WELL_KNOWN_SID_IIS_USERS, FileSystemRights.ReadAndExecute);

                File.WriteAllText(webConfigFile, UtilsSystem.GetEmbededResourceAsString(Assembly.GetExecutingAssembly(), "IIS.offline-web.config"));
                File.WriteAllText(indexFile, UtilsSystem.GetEmbededResourceAsString(Assembly.GetExecutingAssembly(), "IIS.Index.html"));

                var newSite = manager.Sites.Add(offlineSiteName, rootDir, 80);
                newSite.ServerAutoStart = false;

                newSite.Bindings.Remove(newSite.Bindings.First());
                var defaultHostname = UtilsIis.ValidateHostName(this.Deployment.getShortId() + ".offline." + Guid.NewGuid());
                newSite.Bindings.Add($"127.0.0.1:999:{defaultHostname}", "http");

                foreach (var b in this.IisSettings.bindings)
                {
                    this.DeployBinding(newSite, b.Value, "root", true);
                }

                UtilsIis.CommitChanges(manager);
            }
        }

        public void deploySettings(string jsonSettings, string jsonSettingsArray, RuntimeSettingsReplacer replacer)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public void start()
        {
            if (this.Deployment == null)
            {
                return;
            }

            // Small trick, we need to stop the offline site for the previous deployment
            // as there is high chance of binding colision
            if (this.Deployment.GetPreviousDeployment() != null)
            {
                this.AppPoolUtils.WebsiteAction(this.GetOfflineSiteName(this.Deployment.GetPreviousDeployment()), AppPoolActionType.Stop, skipApplicationPools: true);
            }

            this.AppPoolUtils.WebsiteAction(this.GetOfflineSiteName(this.Deployment), AppPoolActionType.Stop, skipApplicationPools: true);
            this.AppPoolUtils.WebsiteAction(this.GetSiteName(this.Deployment), AppPoolActionType.Start);
        }

        /// <summary>
        /// Remove a deployed site
        /// </summary>
        public void undeploy(bool isUninstall = false)
        {
            this.EnsureSslCertificateProviderInitialized();

            // Cannot cleanup and undeploy environment.
            if (this.Deployment == null)
            {
                return;
            }

            this.RemoveCdnRules();

            string siteName = this.GetSiteName(this.Deployment);

            var effectivePoolNames = this.CollectApplicationPoolNames(siteName);

            // Only one site per deployment...
            var applicationPools = this.Deployment.GetSettingCollection<string>("iis.appplicationpools");

            List<string> userProfilesDirsToRemove = new List<string>();

            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, this.GetSiteName(this.Deployment), this.Logger)
                    .SingleOrDefault();

                if (site != null)
                {
                    this.Logger.LogInfo(true, "IIS Deployer: undeploy removing site with name {0} and id {1}", site.Name, site.Id);

                    // Sometimes the site fails with 
                    // Exception Type: 'System.Runtime.InteropServices.COMException
                    // ExceptionMessage: The object identifier does not represent a valid object. (Exception from HRESULT: 0x800710D8)
                    // Stack Trace:   at Microsoft.Web.Administration.Interop.IAppHostProperty.get_Value()
                    // at Microsoft.Web.Administration.ConfigurationElement.GetPropertyValue(IAppHostProperty property)
                    // at Microsoft.Web.Administration.Site.get_State()
                    try
                    {
                        if (site.Bindings.Any() && site.State != ObjectState.Stopped)
                        {
                            this.AppPoolUtils.WebsiteAction(site.Name, AppPoolActionType.Stop);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    UtilsIis.RemoveSite(site, manager, this.Logger);
                    UtilsIis.CommitChanges(manager);
                }
            }

            using (ServerManager manager = new ServerManager())
            {
                // Remove application pools
                foreach (var poolName in applicationPools.Keys)
                {
                    var pool = manager.ApplicationPools.FirstOrDefault(i => i.Name == poolName);

                    if (pool == null)
                    {
                        continue;
                    }

                    this.Logger.LogInfo(true, "IIS Deployer: undeploy removing application pool {0}", pool.Name);

                    var dir = UtilsIis.FindStorageFolderForAppPoolWithDefaultIdentity(pool);

                    if (dir != null)
                    {
                        userProfilesDirsToRemove.Add(dir);
                    }

                    manager.ApplicationPools.Remove(pool);
                }

                UtilsIis.CommitChanges(manager);
            }

            using (ServerManager manager = new ServerManager())
            {
                // Remove the offline site
                var offlineSite = UtilsIis.FindSiteWithName(manager, this.GetOfflineSiteName(this.Deployment), this.Logger).SingleOrDefault();

                if (offlineSite != null)
                {
                    UtilsIis.RemoveSite(offlineSite, manager, this.Logger);
                    UtilsIis.CommitChanges(manager);
                }
            }

            // Clear IIS Compression files and config isolation for the pools
            this.CleanCompressionPath(effectivePoolNames);
            this.CleanPoolIsolationSettings(effectivePoolNames);

            // Clear all local HOSTS file setup
            this.UtilsHosts.RemoveHostsMapping(this.Deployment.getShortId());

            // This is a legacy cleanup, when we used installedApplication identifier for host entry ownership
            this.UtilsHosts.RemoveHostsMapping(this.Deployment.installedApplicationSettings.GetId());

            // Now remove storage for the users of the application pools...
            // this is "delicate"....
            foreach (var dir in userProfilesDirsToRemove)
            {
                // Use utils system here...
                UtilsSystem.DeleteDirectory(dir, this.Logger);
            }

            // Next steps only for uninstall
            if (!isUninstall)
            {
                return;
            }
        }

        /// <summary>
        /// Deploy a binding
        /// </summary>
        /// <param name="site"></param>
        /// <param name="binding"></param>
        /// <param name="type"></param>
        /// <param name="overwrite"></param>
        /// <param name="deploymentMode">If set to true, this will not deploy the bindings, but prepare SSL certificates.</param>
        protected void DeployBinding(
            Site site,
            Binding binding,
            string type,
            bool overwrite = false,
            BindingDeploymentMode deploymentMode = BindingDeploymentMode.Normal)
        {
            string hostName = UtilsIis.ValidateHostName(this.Deployment.GetSettingsReplacer().DoReplace(binding.hostname));
            bool prepareSsl = deploymentMode.HasFlag(BindingDeploymentMode.PrepareSsl);

            if (!prepareSsl)
            {
                if (overwrite)
                {
                    var olds = site.Bindings.Where(

                        // Mismo hostname
                        (i) => hostName.Equals(i.Host, StringComparison.CurrentCultureIgnoreCase)

                        // Mismo puerto
                        && i.EndPoint.Port == binding.port).ToList();

                    foreach (var old in olds)
                    {
                        site.Bindings.Remove(old);
                    }
                }
            }

            NetworkInterface networkinterface = null;

            // Find the address for the binding, or directly an IP
            string address;
            if (binding.@interface == "*")
            {
                address = "*";
            }
            else
            {
                if (!IPAddress.TryParse(binding.@interface, out var ipAddress))
                {
                    networkinterface = this.GlobalSettings.FindEndpointAddress(binding.@interface);

                    if (!IPAddress.TryParse(networkinterface.ip, out ipAddress))
                    {
                        throw new Exception("Specified interface does not match any declared adapter and is not a valid IP address: " + binding.@interface);
                    }
                }

                address = ipAddress.ToString();
            }

            string bindingProtocol = binding.type;
            if (string.IsNullOrWhiteSpace(bindingProtocol))
            {
                bindingProtocol = "http";
            }

            var info = string.Join(":", address, binding.port, hostName);
            var infoWithoutSsl = string.Join(":", address, "80", hostName);

            Microsoft.Web.Administration.Binding siteBinding = null;

            // The local needs a hosts file mapping to work... we map the hosts
            // file now because the SSL validation might need this ASAP.
            if (binding.addtohosts || (networkinterface != null && networkinterface.forcehosts))
            {
                string localHostsAddress = address;

                // Wildcards mean nothing in HOSTS file, so map to the local IP forecefully
                if (localHostsAddress == "*")
                {
                    localHostsAddress = UtilsIis.LOCALHOST_ADDRESS;
                }

                this.UtilsHosts.AddHostsMapping(localHostsAddress, hostName, this.Deployment.getShortId());

                // Keep track of the hosts added by this deployment
                this.Deployment.SetSettingCollection("hosts", info, info);
            }

            bool isSsl = binding.IsSsl();

            // If we are in SSL prepare mode and this is not ssl, simply Skip.
            if (prepareSsl && !isSsl)
            {
                return;
            }

            // Check proper SSL configuration
            if ((binding.ssl_letsencrypt || !string.IsNullOrWhiteSpace(binding.ssl_certificate_friendly_name)) && !isSsl)
            {
                throw new Exception("To use SSL settings, the endpoint must have type=https (SSL).");
            }

            // SSL Setup
            if (isSsl)
            {
                if (binding.ssl_letsencrypt)
                {
                    // Preparation only ensure that a valid SSL certificate has been provisioned
                    if (prepareSsl)
                    {
                        bool fakeSsl = deploymentMode.HasFlag(BindingDeploymentMode.UseSelfSigned);
                        bool forceRenewal = deploymentMode.HasFlag(BindingDeploymentMode.ForceRenewal);

                        if (fakeSsl)
                        {
                            this.Logger.LogWarning(true, "A self-signed certificate is provisioned on first installation of an application. To use let's encrypt, make sure the application is the deployed in all the web nodes, delete the self-signed certificate and redeploy the application on one node.");
                        }

                        // This will throw an exception if it is not able to provision the certificate or find an existing
                        // certificate in the central store
                        this.SslProvisioningManager.ProvisionCertificateInIis(
                            hostName,
                            "info@sabentis.com",
                            infoWithoutSsl,
                            site.Name,
                            fakeSsl,
                            forceRenewal);

                        return;
                    }

                    siteBinding = site.Bindings.Add(info, null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                }
                else if (!string.IsNullOrWhiteSpace(binding.ssl_certificate_friendly_name))
                {
                    // Nothing to prepare as this will be skipped if the certificate is not found
                    if (prepareSsl)
                    {
                        return;
                    }

                    // Here we asume that the given certificate is valid and not expired
                    X509Certificate2 cert = null;
                    X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                    cert = store.Certificates
                        .OfType<X509Certificate2>()
                        .FirstOrDefault(x => x.FriendlyName == binding.ssl_certificate_friendly_name);

                    if (cert == null)
                    {
                        throw new Exception(
                            $"SSL Binding '{info}' will be skipped. Unable to find a valid certificate with friendly name '{binding.ssl_certificate_friendly_name}'.");
                    }

                    siteBinding = site.Bindings.Add(info, cert.GetCertHash(), store.Name, SslFlags.Sni);
                }
                else
                {
                    // Nothing to prepare as this will be skipped if the certificate is not found
                    if (prepareSsl)
                    {
                        return;
                    }

                    // Let's find a certificate in the central certificate store
                    var certificate = UtilsIis.FindCertificateInCentralCertificateStore(hostName, this.Logger, out _);

                    if (certificate == null)
                    {
                        this.Logger.LogError("SSL Binding '{0}' could not be bound to any valid SSL certificate. Unable to find a valid certificate in Central Certificate Store for hostname '{1}'.", info, hostName);

                        // Even if the certificate is not found, leave the site operative...
                        siteBinding = site.Bindings.Add(info, null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                    }
                    else
                    {
                        siteBinding = site.Bindings.Add(info, null, null, SslFlags.CentralCertStore | SslFlags.Sni);
                    }
                }
            }
            else
            {
                siteBinding = site.Bindings.Add(info, bindingProtocol);
            }

            this.Logger.LogInfo(true, "Site binding: '{0}' [protocol={1}, SslFlags.Sni={2}, SslFlags.CentralCertStore={3}, CertificateHash={4}]", siteBinding, siteBinding.Protocol, siteBinding.SslFlags.HasFlag(SslFlags.Sni), siteBinding.SslFlags.HasFlag(SslFlags.CentralCertStore), siteBinding.CertificateHash?.ToString());

            string baseurl = $"{bindingProtocol}://{hostName}";

            if (binding.port != 80)
            {
                baseurl += ":" + binding.port;
            }

            string basekey = $"deployers.{this.IisSettings.id}.bindings.{type}.{binding.id}";

            this.Deployment.SetRuntimeSetting(basekey + ".url", baseurl);
            this.Deployment.SetRuntimeSetting(basekey + ".interface_alias", binding.@interface);
            this.Deployment.SetRuntimeSetting(basekey + ".port", Convert.ToString(binding.port));
            this.Deployment.SetRuntimeSetting(basekey + ".hostname", hostName);
        }

        /// <summary>
        /// Get all pool names asociated with a website
        /// </summary>
        /// <param name="siteName"></param>
        /// <returns></returns>
        protected List<string> CollectApplicationPoolNames(string siteName)
        {
            List<string> poolNames = new List<string>();

            using (ServerManager serverManager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(serverManager, siteName, this.Logger).SingleOrDefault();

                if (site == null)
                {
                    return poolNames;
                }

                foreach (var app in site.Applications)
                {
                    poolNames.Add(app.ApplicationPoolName);
                }
            }

            return poolNames.Distinct().ToList();
        }

        /// <summary>
        /// The path for static file compression in IIS is shared among all sites, although in IIS10 this path is supposed to
        /// be overridable per site, this is not true (tested). Other settings do work.
        /// </summary>
        protected void CleanCompressionPath(List<string> pools)
        {
            // Grab the compression file path
            string staticFileCompressedCachePath = null;

            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    var config = serverManager.GetApplicationHostConfiguration();
                    ConfigurationSection httpCompressionSection = config.GetSection("system.webServer/httpCompression");
                    staticFileCompressedCachePath = Environment.ExpandEnvironmentVariables(httpCompressionSection["directory"]?.ToString());
                }
            }
            catch (Exception e)
            {
                this.Logger.LogWarning(true, "Unable to determine HttpCompression cache path. The feature might not be available in this server.");
                return;
            }

            foreach (var pool in pools)
            {
                string targetDirectory = staticFileCompressedCachePath + "\\" + pool;
                UtilsSystem.DeleteDirectory(targetDirectory, this.Logger);
            }
        }

        protected void CleanPoolIsolationSettings(List<string> pools)
        {
            foreach (var pool in pools)
            {
                // Grab the compression file path
                string isolatedPoolConfigPath = Path.Combine(UtilsIis.GetConfigIsolationPath(), pool);

                if (Directory.Exists(isolatedPoolConfigPath))
                {
                    UtilsSystem.DeleteDirectory(isolatedPoolConfigPath, this.Logger, 5);
                }
            }
        }

        /// <summary>
        /// The name to use for the application pool
        /// </summary>
        /// <param name="pool"></param>
        /// <returns></returns>
        protected string GetPoolIdForDeployment(Pool pool)
        {
            return this.Deployment.getShortId() + "_" + pool.id;
        }

        /// <summary>
        /// Deploy a single application pool. Does not commit changes
        /// to the server manager.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="pool"></param>
        protected void DeployApplicationPool(ServerManager manager, Pool pool, ApplicationLimits limits)
        {
            // Must be careful with application pools. Creating unique pool
            // names will create a unique identity (with it's own user profile)
            // every time. This can consume disk space in the users profile folder
            // (unless we manually remove the old profiles...) but at the same time
            // it might NOT be advisable to reuse the same pool between deployments
            // as they might not have the same setup and thus doing a rollback
            // of the deployment might not work...

            ApplicationPool p = null;

            string poolname = this.GetPoolIdForDeployment(pool);

            this.Deployment.SetSettingCollection("iis.appplicationpools", poolname, poolname);

            p = manager.ApplicationPools.FirstOrDefault(i => i.Name == poolname);

            if (p == null)
            {
                p = manager.ApplicationPools.Add(poolname);
                this.Logger.LogInfo(true, "Created new application pool '{0}'", p.Name);
            }
            else
            {
                this.Logger.LogInfo(true, "Found existing application pool '{0}'", p.Name);
            }

            // Enable x86 or x64 process type
            p.Enable32BitAppOnWin64 = pool.Enable32BitAppOnWin64;

            // Arranque automático.
            p.AutoStart = pool.AutoStart;

            // Modo integrado (en verdad da igual...)
            switch (pool.ManagedPipelineMode)
            {
                case "Integrated":
                case null:
                    p.ManagedPipelineMode = ManagedPipelineMode.Integrated;
                    break;
                case "Classic":
                    p.ManagedPipelineMode = ManagedPipelineMode.Classic;
                    break;
                default:
                    throw new Exception("Unkown pipeline mode:" + pool.ManagedPipelineMode);
            }

            // Managed runtime version needs to be one of the following...
            var admitedruntimeversions = new List<string>()
            {
                "v4.0",
                string.Empty,
                "v2.0"
            };

            if (!admitedruntimeversions.Contains(pool.ManagedRuntimeVersion))
            {
                throw new Exception("Requested pool ManagedRuntimeVersion not supported: " + pool.ManagedRuntimeVersion + ". Use one of the following: " + string.Join(" | ", admitedruntimeversions));
            }

            p.ManagedRuntimeVersion = pool.ManagedRuntimeVersion;

            // Autostart was replaced by startmode in newer
            // versions of IIS. Use SetAttributeValue instead of
            // the dll's attribute because at runtime it crashes
            // on old versions of IIS, even if the call is NOT made.
            if (UtilsIis.GetIisVersion() >= Version.Parse("8.0"))
            {
                string startMode = pool.StartMode;

                // Check if always running is available
                if (startMode == StartMode.AlwaysRunning.ToString() && limits.IisPoolStartupModeAllowAlwaysRunning == false)
                {
                    this.Logger.LogWarning(true, "AlwaysRunning pool start mode downgraded to OnDemand due to Chef Application Limits configuration");
                    startMode = StartMode.OnDemand.ToString();
                }

                switch (startMode)
                {
                    case "OnDemand":
                    case null:
                    case "":
                        p.SetAttributeValue("StartMode", StartMode.OnDemand);
                        break;
                    case "AlwaysRunning":
                        p.SetAttributeValue("StartMode", StartMode.AlwaysRunning);
                        break;
                    default:
                        throw new Exception("Unkown StartMode mode:" + pool.StartMode);
                }
            }

            // Feature introduced in IIS 8.5
            if (UtilsIis.GetIisVersion() >= Version.Parse("8.5"))
            {
                if (string.IsNullOrWhiteSpace(limits.IisPoolIdleTimeoutAction))
                {
                    // Nothing to do
                }

                if (limits.IisPoolIdleTimeoutAction == IdleTimeoutAction.Suspend.ToString())
                {
                    p.ProcessModel.IdleTimeoutAction = IdleTimeoutAction.Suspend;
                }
                else if (limits.IisPoolIdleTimeoutAction == IdleTimeoutAction.Terminate.ToString())
                {
                    p.ProcessModel.IdleTimeoutAction = IdleTimeoutAction.Terminate;
                }
                else
                {
                    throw new Exception("Unsupported pool idle timeout action " + limits.IisPoolIdleTimeoutAction);
                }
            }

            p.ProcessModel.LoadUserProfile = pool.LoadUserProfile;

            switch (pool.IdentityType)
            {
                case "ChefApp":
                case null:
                    p.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                    p.ProcessModel.UserName = this.Deployment.WindowsUsernameFqdn();
                    p.ProcessModel.Password = this.Deployment.GetWindowsPassword();
                    break;
                case "ApplicationPoolIdentity":
                    p.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                    break;
                case "NetworkService":
                    p.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                    break;
                case "LocalService":
                    p.ProcessModel.IdentityType = ProcessModelIdentityType.LocalService;
                    break;
                case "LocalSystem":
                    p.ProcessModel.IdentityType = ProcessModelIdentityType.LocalSystem;
                    break;
                default:
                    if (pool.IdentityType.StartsWith(nameof(ProcessModelIdentityType.SpecificUser) + ":"))
                    {
                        var startIndex = pool.IdentityType.IndexOf(":", StringComparison.Ordinal);
                        var uname = pool.IdentityType.Substring(startIndex, pool.IdentityType.Length - startIndex);

                        var identity = (from a in this.GlobalSettings.accounts
                                        where a.id == uname
                                        select a).FirstOrDefault();

                        if (identity == null)
                        {
                            throw new Exception("Account identity not found: " + uname);
                        }

                        p.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                        p.ProcessModel.UserName = identity.username;
                        p.ProcessModel.Password = identity.password;
                    }
                    else
                    {
                        throw new Exception("Identity type not supported: " + pool.IdentityType);
                    }

                    break;
            }

            long cpuLimitPercent = pool.CpuLimitPercent;
            string cpuLimitAction = pool.CpuLimitAction;

            if (limits.IisPoolMaxCpuLimitPercent > 0)
            {
                cpuLimitPercent = limits.IisPoolMaxCpuLimitPercent.Value;
            }

            if (!string.IsNullOrWhiteSpace(limits.IisPoolCpuLimitAction))
            {
                cpuLimitAction = limits.IisPoolCpuLimitAction;
            }

            if (cpuLimitAction != ProcessorAction.NoAction.ToString())
            {
                p.Cpu.Limit = cpuLimitPercent * 1000;

                if (string.Equals(cpuLimitAction, ProcessorAction.KillW3wp.ToString()))
                {
                    p.Cpu.Action = ProcessorAction.KillW3wp;
                }
                else if (string.Equals(cpuLimitAction, ProcessorAction.NoAction.ToString()))
                {
                    p.Cpu.Action = ProcessorAction.NoAction;
                }
                else if (string.Equals(cpuLimitAction, ProcessorAction.Throttle.ToString()))
                {
                    p.Cpu.Action = ProcessorAction.Throttle;
                }
                else if (string.Equals(cpuLimitAction, ProcessorAction.ThrottleUnderLoad.ToString()))
                {
                    p.Cpu.Action = ProcessorAction.ThrottleUnderLoad;
                }
                else
                {
                    throw new Exception($"Unrecognized CpuLimitAction: '{cpuLimitAction}'");
                }
            }

            // We enforce setting this value no matter what!
            long privateMemoryLimitKb = pool.PrivateMemoryLimitKb;

            if (privateMemoryLimitKb > limits.IisPoolMaxPrivateMemoryLimitKb || privateMemoryLimitKb == 0)
            {
                privateMemoryLimitKb = limits.IisPoolMaxPrivateMemoryLimitKb.Value;
            }

            p.Recycling.PeriodicRestart.PrivateMemory = privateMemoryLimitKb;

            this.Logger.LogInfo(true, "Pool contention settings: CpuLimitPercent: {0}%, CpuLimitAction: {1}, MaxPrivateMemory: {2}", cpuLimitPercent, cpuLimitAction, UtilsSystem.BytesToString(privateMemoryLimitKb * 1024));
        }

        /// <summary>
        /// Global cleanup / cron
        /// </summary>
        public override void cron()
        {
            this.SslCertificateRenewalCheck(false);
        }

        /// <summary>
        /// Verificar el estado de los certificados SSL y renovar si es necesario
        /// </summary>
        /// <param name="forceRenewal"></param>
        public void SslCertificateRenewalCheck(bool forceRenewal = false)
        {
            this.EnsureSslCertificateProviderInitialized();

            this.Logger.LogInfo(true, "{0}: Veryfying SSL bindings for automatic certificate renewal.", this.Deployment.installedApplicationSettings.GetId());

            // Make sure that we have up-to-date certificates
            using (ServerManager manager = new ServerManager())
            {
                string siteName = this.GetSiteName(this.Deployment);

                var site = UtilsIis.FindSiteWithName(manager, siteName, this.Logger).SingleOrDefault();

                if (site == null)
                {
                    this.Logger.LogWarning(false, "Could not find site '{0}' for ssl renewal verification.", siteName);
                    return;
                }

                foreach (var b in this.IisSettings.bindings)
                {
                    var certificateBindingFlags = BindingDeploymentMode.PrepareSsl;

                    if (forceRenewal)
                    {
                        certificateBindingFlags = certificateBindingFlags | BindingDeploymentMode.ForceRenewal;
                    }

                    this.DeployBinding(site, b.Value, "root", true, certificateBindingFlags);
                }

                // No need to commit..because we are actually not changing the bindings, only re-provisioning certificates.
            }
        }

        /// <summary>
        /// Global cleanup / cron
        /// </summary>
        public override void cleanup()
        {
            using (ServerManager manager = new ServerManager())
            {
                bool changed = false;

                // Using QueryEnumerable here has 2 functions:
                // Apply a filter
                // Make sure we don't interact with broken sites
                var notStartedSites = UtilsSystem.QueryEnumerable(
                    manager.Sites,
                    (s) => s.State != ObjectState.Started,
                    (s) => s,
                    (s) => s.Name,
                    this.Logger);

                foreach (var site in notStartedSites)
                {
                    if (!this.Deployment.IsShortId(site.Name))
                    {
                        continue;
                    }

                    // If it's a short ID for this site,
                    // but not the current deployment...
                    // needs to be removed
                    if (site.Name == this.GetSiteName(this.Deployment))
                    {
                        continue;
                    }

                    this.Logger.LogWarning(true, "Cleaned up stuck site: {0}", site.Name);
                    UtilsIis.RemoveSite(site, manager, this.Logger);

                    // Remove!!!
                    foreach (var app in site.Applications.ToList())
                    {
                        var pool = manager
                            .ApplicationPools
                            .FirstOrDefault(i => i.Name == app.ApplicationPoolName);

                        if (pool != null)
                        {
                            this.Logger.LogWarning(true, "Cleaned up stuck pool: {0}", pool.Name);
                            manager.ApplicationPools.Remove(pool);
                        }
                    }

                    changed = true;
                }

                // Now let's just dynamite unused application pools
                // that start with this site's prefix (it would even
                // be safe to dynamite ALL application pools that have no site...)
                foreach (var pool in manager.ApplicationPools.ToList())
                {
                    if (!pool.Name.StartsWith(this.Deployment.GetShortIdPrefix()))
                    {
                        continue;
                    }

                    bool siteFound = false;

                    foreach (var s in manager.Sites)
                    {
                        foreach (var app in s.Applications)
                        {
                            if (app.ApplicationPoolName == pool.Name)
                            {
                                siteFound = true;
                                break;
                            }
                        }

                        if (siteFound)
                        {
                            break;
                        }
                    }

                    if (!siteFound)
                    {
                        changed = true;
                        this.Logger.LogWarning(true, "Cleaned up unused pool: {0}", pool.Name);
                        manager.ApplicationPools.Remove(pool);
                    }
                }

                if (changed)
                {
                    UtilsIis.CommitChanges(manager);
                }
            }
        }

        public void sync()
        {
        }

        /// <summary>
        /// Check if exists a site without SNI when there is an CSS
        /// </summary>
        private void CheckSitesWithoutSni()
        {
            using (ServerManager manager = new ServerManager())
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
                        this.Logger.LogWarning(false, string.Concat("There is a site with CSS and the following sites don't have SNI: ", string.Join(", ", sitesWithoutSni)));
                    }
                }
            }
        }
    }
}
