using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;

namespace iischef.core.IIS
{
    /// <summary>
    /// Helper to setup the CDN site, not quite stand alone yet, drived from the IIS deployer :(
    /// </summary>
    public class CdnHelper
    {
        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// The global settings
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        protected UtilsHosts UtilsHosts;

        /// <summary>
        /// The name that will be used for the IIS site used to mount the CDN
        /// </summary>
        public readonly string CstChefCndSiteName = "__chef_cdn";

        /// <summary>
        /// Internal hostname of the CDN site
        /// </summary>
        public readonly string CstChefInternalHostname = "local.chefcdn.com";

        /// <summary>
        /// 
        /// </summary>
        public CdnHelper(ILoggerInterface logger, EnvironmentSettings globalSettings)
        {
            this.Logger = logger;
            this.GlobalSettings = globalSettings;
            this.UtilsHosts = new UtilsHosts(logger);
        }

        /// <summary>
        /// Get the webroot for the CDN site, initialized with a base web.config prepared for URL REWRITING
        /// </summary>
        /// <returns></returns>
        public string GetCdnWebConfigPathInitialized()
        {
            var basedir =
                UtilsSystem.EnsureDirectoryExists(
                    UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultApplicationStorage().path, "__chef_cdn"), true);

            var webconfigfilepath = UtilsSystem.CombinePaths(basedir, "web.config");

            // Si no hay un web.config plantilla, crearlo ahora.
            if (!File.Exists(webconfigfilepath))
            {
                File.WriteAllText(webconfigfilepath, @"
                        <configuration>
                          <system.webServer>
                            <rewrite>
                              <rules>
                              </rules>
                              <outboundRules>
                              </outboundRules>
                            </rewrite>
                          </system.webServer>
                        </configuration>
                        ");
            }

            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(new SecurityIdentifier(UtilsWindowsAccounts.WELL_KNOWN_SID_USERS), basedir, FileSystemRights.ReadAndExecute);

            // Make sure that the site exists
            using (ServerManager manager = new ServerManager())
            {
                bool configChanged = false;

                var site = UtilsIis.FindSiteWithName(manager, this.CstChefCndSiteName, this.Logger)
                    .FirstOrDefault();

                if (site == null)
                {
                    manager.Sites.Add(this.CstChefCndSiteName, "http", $"{UtilsIis.LOCALHOST_ADDRESS}:80:{this.CstChefInternalHostname}", basedir);

                    configChanged = true;
                }
                else
                {
                    if (site.Applications.First().VirtualDirectories.First().PhysicalPath != basedir)
                    {
                        site.Applications.First().VirtualDirectories.First().PhysicalPath = basedir;
                        configChanged = true;
                    }
                }

                if (configChanged)
                {
                    UtilsIis.CommitChanges(manager);
                }
            }

            this.UtilsHosts.AddHostsMapping(UtilsIis.LOCALHOST_ADDRESS, this.CstChefInternalHostname, "chf_IISDeployer_CDN");

            // Add a cross domain file
            var crossdomainfilepath = UtilsSystem.CombinePaths(Path.GetDirectoryName(webconfigfilepath), "crossdomain.xml");
            File.WriteAllText(crossdomainfilepath, UtilsSystem.GetEmbededResourceAsString(Assembly.GetExecutingAssembly(), "IIS.crossdomain.xml"));

            // Add common proxy headers
            UtilsIis.AddAllowedServerVariablesForUrlRewrite(
                this.CstChefCndSiteName,
                "HTTP_X_FORWARDED_FOR",
                "HTTP_X_FORWARDED_PROTO",
                "HTTP_X_FORWARDED_HOST");

            return webconfigfilepath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rule">The rule</param>
        /// <param name="replaceIfExists">If a rule exists with the same name, replace it, otherwise, throw exception.</param>
        public void AddRewriteRule(string rule, bool replaceIfExists = true)
        {
            var parsedRule = XElement.Parse(rule);

            string name = parsedRule.DescendantsAndSelf("rule").First().Attribute("name").Value;

            var webconfigfilepath = this.GetCdnWebConfigPathInitialized();
            XDocument webcfg = XDocument.Parse(File.ReadAllText(webconfigfilepath));

            XElement ruleselement = webcfg.Root.GetAndEnsureXpath("system.webServer/rewrite/rules");

            var existingRule = (from p in ruleselement.Descendants("rule")
                                where p.Attribute("name")?.Value == name
                                select p).FirstOrDefault();

            if (existingRule != null && !replaceIfExists)
            {
                throw new Exception("Rule already exists");
            }

            existingRule?.Remove();

            // Now add from scratchrule
            ruleselement.Add(parsedRule);

            // Perist it
            UtilsIis.WriteWebConfig(webconfigfilepath, webcfg.ToString());
        }

        /// <summary>
        /// Remove all rewrite rules that start with the given prefix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="logger"></param>
        public void RemoveRewriteRulesWithPrefix(string prefix, ILoggerInterface logger)
        {
            // If there is no CDN site, do nothing
            using (ServerManager manager = new ServerManager())
            {
                var site = UtilsIis.FindSiteWithName(manager, this.CstChefCndSiteName, logger).SingleOrDefault();

                if (site == null)
                {
                    return;
                }
            }

            var webconfigfilepath = this.GetCdnWebConfigPathInitialized();
            XDocument webcfg = XDocument.Parse(File.ReadAllText(webconfigfilepath));

            var rules = (from p in webcfg.Descendants("rule")
                         where p.Attribute("name")?.Value?.StartsWith(prefix) == true
                         select p).ToList();

            foreach (var rule in rules)
            {
                rule?.Remove();
            }

            UtilsIis.WriteWebConfig(webconfigfilepath, webcfg.ToString());
        }

        /// <summary>
        /// Add the cache buster rewrite rule
        /// </summary>
        public void AddCacheBusterRewriteRule()
        {
            // Add a default cache booster rewrite URL
            var cacheBusterRule = @"
                 <rule name=""Chef Cache Buster"">
                    <match url=""^cachebuster_\d*/(.*)"" />
                    <action type=""Rewrite"" url=""{R:1}"" />
                </rule>
       ";
            this.AddRewriteRule(cacheBusterRule, true);

            this.SetRuleIndex("Chef Cache Buster", 0);
        }

        public void ConfigureProxy()
        {
            // For this local CDN to work we need IIS-ARR installed and configured at the IIS level, otherwise
            // IIS gets stuck with this config (+ it won't work)
            try
            {
                // Ensure that proxy is enabled and available at the IIS level.
                // This needs the IIS Application Request Routing extension.
                using (ServerManager manager = new ServerManager())
                {
                    bool configChanged = false;

                    var config = manager.GetApplicationHostConfiguration();

                    ConfigurationSection proxySection = config.GetSection("system.webServer/proxy");

                    // Disable reverseRewriteHostInResponseHeaders
                    if (!bool.TryParse(proxySection["reverseRewriteHostInResponseHeaders"]?.ToString(), out var proxyReverseRewrite) || proxyReverseRewrite == true)
                    {
                        proxySection["reverseRewriteHostInResponseHeaders"] = false;
                        configChanged = true;
                    }

                    // Enable proxy functionality
                    if (!bool.TryParse(proxySection["enabled"]?.ToString(), out var proxyEnabled) || proxyEnabled == false)
                    {
                        proxySection["enabled"] = true;
                        configChanged = true;
                    }

                    // Disable disk cache
                    ConfigurationElement cacheElement = proxySection.GetChildElement("cache");

                    if (!bool.TryParse(cacheElement["enabled"]?.ToString(), out var cacheEnabled) || cacheEnabled == true)
                    {
                        cacheElement["enabled"] = false;
                        configChanged = true;
                    }

                    if (configChanged)
                    {
                        this.Logger.LogWarning(false, "Your IIS-ARR settings have been updated to work with Chef CDN: [proxy.enabled=true] && [proxy.cache.enabled=false]");
                        UtilsIis.CommitChanges(manager);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Could not configure server-wide proxy settings for CDN related functionality. Make sure that the chocolatey iis-arr package is installed.", e);
            }
        }

        /// <summary>
        /// Move a rule to the requested INDEX in the set of rules
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="index"></param>
        protected void SetRuleIndex(string ruleName, int index)
        {
            var webconfigfilepath = this.GetCdnWebConfigPathInitialized();

            XDocument webcfg = XDocument.Parse(File.ReadAllText(webconfigfilepath));

            XElement ruleselement = webcfg.Root.GetAndEnsureXpath("system.webServer/rewrite/rules");

            XElement existingRule = null;

            var nodes = ruleselement.Nodes().ToList();

            foreach (var node in nodes.ToList())
            {
                if (node is XElement xNode && xNode.Attribute("name")?.Value == ruleName)
                {
                    if (nodes.IndexOf(xNode) == index)
                    {
                        return;
                    }

                    existingRule = xNode;
                    nodes.Remove(xNode);
                    break;
                }
            }

            if (existingRule == null)
            {
                throw new Exception($"Rule {ruleName} not found.");
            }

            nodes.Insert(index, existingRule);

            ruleselement.ReplaceAll(nodes.ToArray());

            UtilsIis.WriteWebConfig(webconfigfilepath, webcfg.ToString());
        }
    }
}
