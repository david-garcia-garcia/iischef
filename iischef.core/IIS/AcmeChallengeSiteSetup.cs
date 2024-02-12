using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using System;
using System.IO;
using System.Linq;

namespace iischef.core.IIS
{
    public class AcmeChallengeSiteSetup
    {
        /// <summary>
        /// Site name used to centralized ACME challenge responses
        /// </summary>
        public const string AcmeChallengeSiteName = "__acmechallenge";

        /// <summary>
        /// 
        /// </summary>
        protected readonly ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public AcmeChallengeSiteSetup(ILoggerInterface logger)
        {
            this.Logger = logger;
        }

        /// <summary>
        /// Get a shared site root for the application
        /// </summary>
        /// <returns></returns>
        public static string GetAcmeCentralSiteRoot(bool throwIfMissing = true)
        {
            using (ServerManager sm = new ServerManager())
            {
                var existingSite = (from p in sm.Sites
                                    where p.Name == AcmeChallengeSiteName
                                    select p).FirstOrDefault();

                if (existingSite != null)
                {
                    return existingSite.Applications.First().VirtualDirectories.First().PhysicalPath;
                }
            }

            if (throwIfMissing)
            {
                throw new BusinessRuleException($"ACME Shared Site not yet setup. Run IISChefSetupAcmeChallenge first or look for the IIS site {AcmeChallengeSiteName}");
            }

            return null;
        }

        /// <summary>
        /// The idea is to have a proxied site in IIS that points to a central physical store,
        /// handle the verification token if available, if not, proxy back to the real site (so that
        /// this does not affect any ad-hoc acme validation performed by site users).
        /// </summary>
        public void SetupAcmeChallengeSite(string sharedPath = null)
        {
            // We need the static HTTP feature to be enaBLE
            if (!UtilsSystem.IsWindowsFeatureEnabled(IISFeatureNames.StaticContent, this.Logger))
            {
                throw new BusinessRuleException("Static content handler must be enabled in this server.");
            }

            if (string.IsNullOrWhiteSpace(sharedPath))
            {
                sharedPath = GetAcmeCentralSiteRoot(false);

                if (sharedPath == null)
                {
                    var ccsPath = UtilsIis.GetCentralCertificateStorePath(this.Logger);
                    sharedPath = Path.Combine(ccsPath, "acme");
                    UtilsSystem.DirectoryCreateIfNotExists(sharedPath);
                    this.Logger.LogInfo(false, $"Automatically setting acme site path to {sharedPath}.");
                }
            }

            if (!Directory.Exists(sharedPath))
            {
                throw new BusinessRuleException($"The specified shared path {sharedPath} does not exist.");
            }

            UtilsIis.ConfigureProxy(this.Logger);

            bool changed = false;

            // *********************************************
            // Centralized challenge site proxy
            // *********************************************

            using (ServerManager sm = new ServerManager())
            {
                var existingSite = (from p in sm.Sites
                                    where p.Name == AcmeChallengeSiteName
                                    select p).FirstOrDefault();

                // Utilizamos un puerto para evitar el uso de un fichero HOSTS ficticio local. El motivo
                // es que en un contendor el ficheros de HOSTS... más vale no tocarlo, por experiencia.
                string bindingInfo = "*:8095:*";

                if (existingSite == null)
                {
                    existingSite = sm.Sites.Add(AcmeChallengeSiteName, "http", bindingInfo, sharedPath);
                    changed = true;
                }

                // Setup an application pool
                ApplicationPool pool = null;
                pool = sm.ApplicationPools.FirstOrDefault(i => i.Name == AcmeChallengeSiteName);

                if (pool == null)
                {
                    pool = sm.ApplicationPools.Add(AcmeChallengeSiteName);
                    changed = true;
                }

                if (pool.Enable32BitAppOnWin64 != false)
                {
                    pool.Enable32BitAppOnWin64 = false;
                    changed = true;
                }

                if (pool.AutoStart != true)
                {
                    pool.AutoStart = true;
                    changed = true;
                }

                if (pool.StartMode != StartMode.AlwaysRunning)
                {
                    pool.StartMode = StartMode.AlwaysRunning;
                    changed = true;
                }

                if (pool.ProcessModel.LoadUserProfile != false)
                {
                    pool.ProcessModel.LoadUserProfile = false;
                    changed = true;
                }

                if (existingSite.Applications.First().ApplicationPoolName != pool.Name)
                {
                    existingSite.Applications.First().ApplicationPoolName = pool.Name;
                    changed = true;
                }

                if (existingSite.Bindings.All(i => i.BindingInformation != bindingInfo))
                {
                    existingSite.Bindings.Add(bindingInfo, "http");
                    changed = true;
                }

                if (existingSite.Applications.First().VirtualDirectories.First().PhysicalPath != sharedPath)
                {
                    this.Logger.LogWarning(false, $"Shared acme store set to {sharedPath}");

                    existingSite.Applications.First().VirtualDirectories.First().PhysicalPath = sharedPath;
                    changed = true;
                }

                var sourceDir = UtilsSystem.FindResourcePhysicalPath(typeof(SslCertificateProviderService), ".well-known/acme-challenge/web.config");
                File.Copy(sourceDir, Path.Combine(sharedPath, "web.config"), true);

                if (changed)
                {
                    this.Logger.LogWarning(true, $"Saving site changes to: {existingSite.Name}");
                    UtilsIis.CommitChanges(sm);
                }
            }

            UtilsIis.ConfigureAnonymousAuthForIisApplicationToUsePool(AcmeChallengeSiteName);

            var poolUtils = new UtilsAppPool(this.Logger);

            poolUtils.WebsiteAction(AcmeChallengeSiteName, AppPoolActionType.Start);

            // *********************************************
            // Global proxy rule
            // *********************************************

            using (ServerManager sm = new ServerManager())
            {
                changed = false;
                var config = sm.GetApplicationHostConfiguration();

                ConfigurationSection globalRules = config.GetSection("system.webServer/rewrite/globalRules");
                ConfigurationElementCollection globalRulesCollection = globalRules.GetCollection();

                ConfigurationElement rule = globalRulesCollection.FindOrCreateElement("rule", ref changed, "name", @".acme-challenge");

                rule.EnsureElementAttributeValue("enabled", true, ref changed);
                rule.EnsureElementAttributeValue("stopProcessing", true, ref changed);
                rule.EnsureElementAttributeValue("patternSyntax", "ECMAScript", ref changed);

                var match = rule.GetChildElement("match");
                match.EnsureElementAttributeValue("url", "^\\.well-known/acme-challenge/(.*)$", ref changed);

                var conditions = rule.GetChildElement("conditions");
                conditions.EnsureElementAttributeValue("trackAllCaptures", true, ref changed);
                ConfigurationElementCollection conditionsCollection = conditions.GetCollection();

                conditionsCollection.FindOrCreateElement("add", ref changed, "input", "{CACHE_URL}")
                    .EnsureElementAttributeValue("pattern", "^(https?\\:)\\/\\/([^\\:\\/\\|]*)(\\:([0-9]*))?(\\:([^:^\\/]*))?(\\/.*)?$", ref changed)
                    .EnsureElementAttributeValue("negate", false, ref changed);

                conditionsCollection.FindOrCreateElement("add", ref changed, "input", "{HTTP_X_ORIGINALCACHEURL}")
                    .EnsureElementAttributeValue("pattern", "(.|\\s)*\\S(.|\\s)*", ref changed)
                    .EnsureElementAttributeValue("negate", true, ref changed);

                // Do not self redirect
                conditionsCollection.FindOrCreateElement("add", ref changed, "input", "{C:2}")
                    .EnsureElementAttributeValue("pattern", "^local\\.acmechallenge$", ref changed)
                    .EnsureElementAttributeValue("negate", true, ref changed);

                ConfigurationElement actionElement = rule.GetChildElement("action");
                actionElement.EnsureElementAttributeValue("type", 1, ref changed); // 1 == "Rewrite"
                actionElement.EnsureElementAttributeValue("url", @"http://localhost:8095/{HTTP_HOST}{C:7}", ref changed);

                var serverVariables = rule.GetChildElement("serverVariables");
                var serverVariablesCollection = serverVariables.GetCollection();

                serverVariablesCollection.FindOrCreateElement("set", ref changed, "name", "HTTP_X_ORIGINALCACHEURL");
                serverVariablesCollection.FindOrCreateElement("set", ref changed, "name", "HTTP_X_SKIPACMECHALLENGE");

                if (changed)
                {
                    UtilsIis.CommitChanges(sm);
                }
            }
        }
    }
}
