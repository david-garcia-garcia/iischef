using iischef.core.Configuration;
using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Linq;

namespace iischef.core.Php
{
    public class PhpDeployer : DeployerBase, IDeployerInterface
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

        /// <summary>
        /// 
        /// </summary>
        protected PhpEnvironment PhpSettings;

        public override void initialize(
                EnvironmentSettings globalSettings,
                JObject deployerSettings,
                Deployment deployment,
                ILoggerInterface logger,
                InstalledApplication inhertApp)
        {
            base.initialize(globalSettings, deployerSettings, deployment, logger, inhertApp);
            this.PhpSettings = deployerSettings.castTo<PhpEnvironment>();
        }

        protected string GetIniFilePath()
        {
            return UtilsSystem.CombinePaths(this.Deployment.runtimePath, "php", "php.ini");
        }

        protected string GetFastCgiExe()
        {
            return UtilsSystem.CombinePaths(this.Deployment.runtimePath, "php", "php-cgi.exe");
        }

        protected string GetPhpExe()
        {
            return UtilsSystem.CombinePaths(this.Deployment.runtimePath, "php", "php.exe");
        }

        /// <summary>
        /// A local writable temporary directory
        /// </summary>
        /// <returns></returns>
        protected string GetSysTempDir()
        {
            return UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.Deployment.tempPathSys, "sys_temp_dir"), true);
        }

        public void deploy()
        {
            this.DeployRuntime();
            this.DeployFasctCgi();
            this.DeployPhpAutoloader();
        }

        /// <summary>
        /// Shared runtimes are application agnostic...
        /// </summary>
        /// <returns></returns>
        public void DeployRuntime()
        {
            var runtimePath = this.Deployment.runtimePath;

            IniFileManager inimanager = null;

            // Process the deployment operations
            foreach (KeyValuePair<string, JToken> operation in this.PhpSettings.runtime)
            {
                string type = operation.Value["type"].ToString();
                switch (type)
                {
                    case "dl":
                        var config = operation.Value.castTo<Operations.ItemDownloaderConfig>();
                        var o = new Operations.ItemDownloader(this.Logger, config, this.Deployment.appPath);
                        o.Execute(runtimePath);
                        break;
                    case "ini":
                        // Inimanager needs to be initialized after the runtime has been downloaded (dl operation)
                        inimanager = inimanager ?? new IniFileManager(this.GetIniFilePath(), this.Logger);
                        var o2 = operation.Value.castTo<Operations.IniFileSettings>();

                        // Runtime writable was introduced at a later time, add this workaround
                        // for the most common used values.
                        if (o2.key.Contains("wincache.filemapdir") || o2.key.Contains("opcache.file_cache"))
                        {
                            o2.value = o2.value.Replace("%RUNTIME%", "%RUNTIME_WRITABLE%");
                        }

                        o2.execute(inimanager, this.Deployment);
                        break;
                    case "file":
                        var o3 = operation.Value.castTo<Operations.FileOperation>();
                        o3.execute(runtimePath);
                        break;
                    default:
                        throw new Exception($"Operation type {type} not found.");
                }
            }

            // Add recommended PHP defaults
            inimanager.UpdateOrCreateDirective("expose_php", "Off");
            inimanager.UpdateOrCreateDirective("mail.add_x_header", "Off");

            if (string.IsNullOrWhiteSpace(inimanager.GetValue("disable_functions")))
            {
                inimanager.UpdateOrCreateDirective("disable_functions", "exec,passthru,shell_exec,system,proc_open,popen,curl_exec,curl_multi_exec,parse_ini_file,show_source");
            }

            // doc_root
            inimanager.Save();

            // Preparar variables de entorno
            this.SetupEnvironmentVariables();

            // Configure handlers in IIS's web.config
            this.deployIISHandlers();

            // Deploy a command line shortcut for the PHP environment
            this.DeployPhpRuntimeShortcut();
        }

        /// <summary>
        /// Add an environment variable to the php runtime.
        /// 
        /// It will be loaded in both fast-cgi and console.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected void AddEnvironmentSetting(string key, string value)
        {
            var environmentVariables =
                this.Deployment.GetSetting<Dictionary<string, string>>(
                    "php_environment_variables",
                    new Dictionary<string, string>(), 
                    this.Logger);

            if (environmentVariables.ContainsKey(key))
            {
                return;
            }

            environmentVariables.Add(key, value);

            this.Deployment.SetSetting("php_environment_variables", environmentVariables);
        }

        protected Dictionary<string, string> GetEnvironmentVariables()
        {
            return this.Deployment.GetSetting<Dictionary<string, string>>(
                "php_environment_variables",
                new Dictionary<string, string>(), 
                this.Logger);
        }

        /// <summary>
        /// Environment variables setup...
        /// </summary>
        protected void SetupEnvironmentVariables()
        {
            if (this.PhpSettings.environmentVariables != null)
            {
                foreach (var p in this.PhpSettings.environmentVariables)
                {
                    this.AddEnvironmentSetting(p.Key, p.Value);
                }
            }

            this.AddEnvironmentSetting("PHPRC", Path.GetDirectoryName(this.GetIniFilePath()));
            this.AddEnvironmentSetting("TMP", this.GetSysTempDir());
            this.AddEnvironmentSetting("TEMP", this.GetSysTempDir());
            this.AddEnvironmentSetting("CHEF_RUNTIME_PATH", this.Deployment.runtimePath);
            this.AddEnvironmentSetting("CHEF_RUNTIME_PATH_WRITABLE", this.Deployment.runtimePathWritable);

            string userDrive = Path.Combine(this.Deployment.runtimePathWritable, "HOME");
            Directory.CreateDirectory(userDrive);

            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), userDrive, FileSystemRights.Read, this.GlobalSettings.directoryPrincipal);

            // Automatic HOMEDRIVE and HOMEPATH
            this.AddEnvironmentSetting("HOMEDRIVE", Path.GetPathRoot(userDrive));
            this.AddEnvironmentSetting("HOMEPATH", userDrive.Replace(Path.GetPathRoot(userDrive), string.Empty));
        }

        /// <summary>
        /// Escape a php literal to be embeded between single quotes.
        /// </summary>
        /// <param name="literal"></param>
        /// <returns></returns>
        protected string EscapePhpLiteral(string literal)
        {
            return literal.Replace("'", "\\'")
                .Replace("\\", "\\\\");
        }

        /// <summary>
        /// We need a way to consistently configure PHP settings (such as environment variables)
        /// accross environments in an easy way. Use auto_prepend_file for that....
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters should start on line after declaration", Justification = "Format")]
        protected void DeployPhpAutoloader()
        {
            var iniManager = new IniFileManager(this.GetIniFilePath(), this.Logger);
            var phpIniAutoprependFile = this.Deployment.ExpandPaths(iniManager.GetValue("auto_prepend_file"));

            // Set a new autoprepend....
            var autoPrependDir = Path.Combine(this.Deployment.runtimePath, "php_autoprepend_file");
            Directory.CreateDirectory(autoPrependDir);

            // Autoprepend the default value....
            if (!string.IsNullOrWhiteSpace(phpIniAutoprependFile))
            {
                File.WriteAllText(Path.Combine(autoPrependDir, "default_auto_prepend.php"), @"<?php include_once '" + phpIniAutoprependFile.Replace("'", "\\'") + "';");
            }

            // Autoprepend the environment variables.... these are NOT used to override any existing
            // settings, but to populate when missing.
            var environmentVariables = this.GetEnvironmentVariables();

            // Create a prepend file to populate missing environemnt variables
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?php");
            foreach (var env in environmentVariables)
            {
                sb.AppendLine(string.Format(
                    "if (($val = getenv('{0}')) && empty($val)) {{ putenv ('{0}={1}'); }}",
                    this.EscapePhpLiteral(env.Key),
                    this.EscapePhpLiteral(env.Value)));
            }

            File.WriteAllText(Path.Combine(autoPrependDir, "chef_environment_autoprepend.php"), sb.ToString());

            string autoprependfile = Path.Combine(this.Deployment.runtimePath, "auto_prepend_file.php");

            File.WriteAllText(autoprependfile, string.Format(@"<?php
  $folder = '{0}';
  foreach (glob(""{{$folder}}\*.php"") as $filename) {{
    include $filename;
  }}
", autoPrependDir));

            iniManager.UpdateOrCreateDirective("auto_prepend_file", autoprependfile);
            iniManager.Save();
        }

        /// <summary>
        /// Creates a script to quick open a console to the PHP environment
        /// of the website.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters should start on line after declaration", Justification = "Format")]
        protected void DeployPhpRuntimeShortcut()
        {
            string command = $"{this.GetPhpExe()} -c \"{this.GetIniFilePath()}\" %*";

            var destionationDir = UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.Deployment.runtimePath, "include_path"), true);

            File.WriteAllText(UtilsSystem.CombinePaths(destionationDir, "php.bat"), command);

            File.WriteAllText(
                UtilsSystem.CombinePaths(destionationDir, "setenv.bat"),
                string.Format(
                    @"
set path={0};%path%
cd /D ""{1}""
",
                    destionationDir.Replace("\"", "\"\""),
                    this.Deployment.appPath.Replace("\"", "\"\"")));

            File.WriteAllText(
                UtilsSystem.CombinePaths(destionationDir, "setenv.ps1"),
                string.Format(
                    @"
$Env:Path=""{0};$($Env:Path)"";
CD ""{1}""
",
                    destionationDir.Replace("\"", "\"\""),
                    this.Deployment.appPath.Replace("\"", "\"\"")));

            File.WriteAllText(
                UtilsSystem.CombinePaths(destionationDir, "launch_console.bat"),
                "cmd /k setenv.bat");

            File.WriteAllText(UtilsSystem.CombinePaths(destionationDir, "launch_console_admin_UAC.bat"),
                @"

@echo off
set _SCRIPT_DRIVE=%~d0
set _SCRIPT_PATH=%~p0

call :isAdmin

if %errorlevel% == 0 (
   goto :run
) else (
   echo Requesting administrative privileges...
   goto :UACPrompt
)

exit /b

:isAdmin
   fsutil dirty query %systemdrive% >nul
exit /b

:run
 REM <YOUR BATCH CODE GOES HERE>
 %_SCRIPT_DRIVE%
 cd %_SCRIPT_PATH%
 cmd /k setenv.bat
exit /b

:UACPrompt
  echo Set UAC = CreateObject^(""Shell.Application""^) > ""%temp%\getadmin.vbs""
  echo UAC.ShellExecute ""cmd.exe"", ""/c %~s0 %~1"", """", ""runas"", 1 >> ""%temp%\getadmin.vbs""

  ""%temp%\getadmin.vbs""
  del ""%temp%\getadmin.vbs""
 exit / B`
");
        }

        /// <summary>
        /// Deploy's the IIS PHP handler at the application hosts level, but site specific.
        /// </summary>
        protected void deployIISHandlers()
        {
            using (ServerManager serverManager = new ServerManager())
            {
                var siteName = this.Deployment.getShortId();

                string siteAlias = this.Deployment.getShortId();

                // fastCgi settings in IIS can only be set at the HOSTS level
                // we found no way to set this at a web.config level.
                Microsoft.Web.Administration.Configuration config = serverManager.GetApplicationHostConfiguration();

                ConfigurationElement cfs = null;

                ConfigurationSection section;
                ConfigurationElementCollection elems;

                section = config.GetSection("system.webServer/handlers", siteName);
                elems = section.GetCollection();

                cfs = elems.CreateElement("add");

                cfs.SetAttributeValue("name", "php-" + this.Deployment.getShortId());
                cfs.SetAttributeValue("path", "*.php");
                cfs.SetAttributeValue("verb", "GET,HEAD,POST,PUT,PATCH,DELETE");
                cfs.SetAttributeValue("modules", "FastCgiModule");
                cfs.SetAttributeValue("scriptProcessor", this.GetFastCgiExe() + "|" + this.Deployment.getShortId());
                cfs.SetAttributeValue("resourceType", "Either");
                cfs.SetAttributeValue("requireAccess", "Script");
                cfs.SetAttributeValue("responseBufferLimit", 0);

                // Add as the first handler... order matters here.
                elems.AddAt(0, cfs);

                // And index.php as a default document...
                var files = config.GetSection("system.webServer/defaultDocument", siteName).GetChildElement("files");
                elems = files.GetCollection();

                // We might have inherited settings from a higher level
                // that already cover the default document configuration.
                var exists = (from p in elems
                              where p.Schema.Name == "add"
                              && ((string)p.GetAttributeValue("value")) == "index.php"
                              select 1).Any();

                if (!exists)
                {
                    // TODO: This fails if the default document is already configured at a higher level. Ensure it does
                    // not exist before trying to create it!
                    cfs = elems.CreateElement("add");
                    cfs.SetAttributeValue("value", "index.php");
                    elems.Add(cfs);
                }

                UtilsIis.CommitChanges(serverManager);
            }
        }

        /// <summary>
        /// Old implementation that uses direct manipulation of web.config
        /// </summary>
        protected void deployIISHandlers_old()
        {
            string webConfigPath = this.Deployment.GetSetting<string>(IIS.IISDeployer.CST_SETTINGS_WEBROOT, null, this.Logger);

            if (!File.Exists(webConfigPath))
            {
                throw new Exception("Cannot deploy PHP handlers, web.config file not found at:" + webConfigPath);
            }

            var contents = File.ReadAllText(webConfigPath);
            var xWebConfig = XDocument.Parse(contents);

            var xHandlers = xWebConfig.DescendantsExtended("system.webserver")
                .DescendantsExtended("handlers").FirstOrDefault();

            if (xHandlers == null)
            {
                xHandlers = new XElement("handlers");

                xWebConfig.DescendantsExtended("system.webserver").First().Add(xHandlers);
            }

            // TODO: This should not be such an aggresive strategy, we should build on top of
            // the current web.config in an incremental way so that any other comonent
            // deploying it's own handlers will NOT get overwriten.
            xHandlers.Add(new XElement("clear"));

            // Remove all descendants.
            xHandlers.Add(
                XElement.Parse(
                    string.Format(
                         "<add name=\"php-{0}\" path=\"*.php\" verb=\"GET,HEAD,POST,PUT,PATCH,DELETE\" modules=\"FastCgiModule\" scriptProcessor=\"{1}|{0}\" resourceType=\"Either\" requireAccess=\"Script\" responseBufferLimit=\"0\" />",
                         this.Deployment.getShortId(),
                         this.GetFastCgiExe())));

            xHandlers.Add(
            XElement.Parse(
                string.Format(
                     "<add name=\"StaticFile\" path=\"*\" verb=\"*\" modules=\"StaticFileModule,DefaultDocumentModule,DirectoryListingModule\" resourceType=\"Either\" requireAccess=\"Read\" />",
                     this.Deployment.getShortId(),
                     this.GetFastCgiExe())));

            // Make sure we add the default document here ??
            var xDefaultDocument = xWebConfig.DescendantsExtended("system.webserver")
                .DescendantsExtended("defaultDocument").FirstOrDefault();

            if (xDefaultDocument == null)
            {
                xDefaultDocument = new XElement("defaultDocument");
                xWebConfig.DescendantsExtended("system.webserver").First().Add(xDefaultDocument);
            }

            var xFiles = xDefaultDocument.DescendantsExtended("system.webserver")
                .DescendantsExtended("files").FirstOrDefault();

            if (xFiles == null)
            {
                xFiles = new XElement("files");
                xDefaultDocument.Add(xFiles);
            }

            xFiles.Descendants().Remove();

            // TODO: This should not be such an aggresive strategy, we should build on top of
            // the current web.config in an incremental way so that any other comonent
            // deploying it's own handlers will NOT get overwriten.
            xFiles.Add(new XElement("clear"));

            // Remove all descendants.
            // TODO: The default document thing here is a little bit
            // tricky as doing this prevents the default document
            // defined in the nested APP from working. We should actually
            // try to reverse merge any default document section
            xFiles.Add(XElement.Parse("<add value=\"index.php\"/>"));

            File.WriteAllText(webConfigPath, xWebConfig.ToString());
        }

        /// <summary>
        /// Set environment variables for a console environment.
        /// </summary>
        /// <param name="command"></param>
        public new void deployConsoleEnvironment(StringBuilder command)
        {
            var environmentVariables = this.GetEnvironmentVariables();
            foreach (var p in environmentVariables)
            {
                command.AppendLine(string.Format("$Env:{0} = \"{1}\"", p.Key, p.Value
                    .Replace("\"", "\"\"")));
            }
        }

        /// <summary>
        /// Deploy fast-cgi settings
        /// </summary>
        protected void DeployFasctCgi()
        {
            var limits = this.Deployment.GetApplicationLimits();

            using (ServerManager serverManager = new ServerManager())
            {
                this.Logger.LogWarning(false, "Deploying fastCgi based applications causes IIS to internally reset to pick the new configuration because fastCgi is configured at the server level.");

                var phpRuntime = this.GetFastCgiExe();
                var iniFilePath = this.GetIniFilePath();
                var phpIniFile = this.GetIniFilePath();

                string siteAlias = this.Deployment.getShortId();

                // fastCgi settings in IIS can only be set at the HOSTS level
                // we found no way to set this at a web.config level.
                Microsoft.Web.Administration.Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection section = config.GetSection("system.webServer/fastCgi");
                ConfigurationElement cfs = null;

                // Each fastCgi in IIS is a unique combination of RUNTIME_PATH|ARGUMENTS, try to find
                // the current application.
                foreach (ConfigurationElement sec in section.GetCollection())
                {
                    // Cada aplicación se identifica de manera única por la combincación de atributo y path de ejecución.
                    if (sec.HasValue("arguments", siteAlias) && sec.HasValue("fullPath", phpRuntime))
                    {
                        cfs = sec;
                        break;
                    }
                }

                // We need to keep track if the element already existed
                // in the configuration, or it is new.
                bool addApplication = false;
                ConfigurationElementCollection elems = section.GetCollection();
                if (cfs == null)
                {
                    cfs = elems.CreateElement("application");
                    addApplication = true;
                }

                // In this deployment we are not really passing
                // any argments to PHP, simply use the site Alias to
                // isolate each PHP site. 
                // OJO: PONER EL SITE ALIAS AQUÍ NO ES ALGO
                // GRATUITO. LUEGO EN EL WEB.CONFIG DE LA PROPIA
                // APLICACIÓN DEBE ESTAR EXACTAMENTE IGUAL.
                cfs.SetAttributeValue("arguments", siteAlias);

                // Set reasonable defaults, even if the user configuration says differently
                var instanceMaxRequests = this.PhpSettings.instanceMaxRequests > 200 ? this.PhpSettings.instanceMaxRequests : 10000;
                var maxInstances = this.PhpSettings.maxInstances > 3 ? this.PhpSettings.maxInstances : 10;
                var activityTimeout = this.PhpSettings.activityTimeout > 100 ? this.PhpSettings.activityTimeout : 600;
                var requestTimeout = this.PhpSettings.requestTimeout > 60 ? this.PhpSettings.requestTimeout : 300;

                // Ensure that all values are within the limits
                if (maxInstances > limits.FastCgiMaxInstances && limits.FastCgiMaxInstances > 0)
                {
                    maxInstances = limits.FastCgiMaxInstances.Value;
                }

                // Runtime Path.
                cfs.SetAttributeValue("fullPath", phpRuntime);
                cfs.SetAttributeValue("maxInstances", maxInstances);
                cfs.SetAttributeValue("activityTimeout", activityTimeout);
                cfs.SetAttributeValue("requestTimeout", requestTimeout);
                cfs.SetAttributeValue("instanceMaxRequests", instanceMaxRequests);

                // Make sure that changes to PHP.ini are refreshed properly
                if (File.Exists(iniFilePath))
                {
                    cfs.SetAttributeValue("monitorChangesTo", iniFilePath);
                }

                // Este setting no sirve para nada según -E- de MS porque
                // la implementación de FastCGI está mal hecha en IIS.
                // Los eventos internos de señal no se llegan a ejecutar nunca,
                // lo único que consigues es demorar el cierre de instancias.
                cfs.SetAttributeValue("signalBeforeTerminateSeconds", 0);

                if (!File.Exists(phpIniFile))
                {
                    throw new Exception("PHP.ini file not found. This will break the IIS FastCgiModule when using monitorChangesTo feature.");
                }

                // Retrieve the environment variables.
                ConfigurationElement cfgEnvironment = cfs.GetChildElement("environmentVariables");
                ConfigurationElementCollection a = cfgEnvironment.GetCollection();

                // This is fastcgi specific.
                a.AddOrUpdateConfigurationElementInCollection("PHP_FCGI_MAX_REQUESTS", instanceMaxRequests.ToString());

                // Add all the environment variables.
                var environmentVariables = this.GetEnvironmentVariables();
                foreach (var p in environmentVariables)
                {
                    a.AddOrUpdateConfigurationElementInCollection(p.Key, p.Value);
                }

                if (addApplication)
                {
                    elems.Add(cfs);
                }

                // Cleanup any fastCgi applications that point to non-existent handlers
                // see the comments in FastCgiRemove() as to why this is here.
                var fastCgiHandlers = section.GetCollection();
                foreach (ConfigurationElement sec in fastCgiHandlers.ToList())
                {
                    if (sec.RawAttributes.Keys.Contains("fullPath"))
                    {
                        string fullPath = sec.GetAttributeValue("fullPath").ToString();
                        if (!File.Exists(fullPath))
                        {
                            this.Logger.LogInfo(true, "Removed stale fastCgi handler {0}", fullPath);
                            fastCgiHandlers.Remove(sec);
                        }
                    }
                }

                UtilsIis.CommitChanges(serverManager);
            }
        }

        public void deploySettings(
                string jsonSettings,
                string jsonSettingsNested,
                RuntimeSettingsReplacer replacer)
        {
        }

        public void start()
        {
        }

        public void stop()
        {
        }

        /// <summary>
        /// Remove fastCgi settings
        /// </summary>
        public void undeploy(bool isUninstall = false)
        { 
        }

        /// <summary>
        /// This was removed from the UNDEPLOY process, because any changes to fastCgi at the server
        /// level reset ALL sites in IIS, meaning that every time you deploy a fastCgi based application
        /// at least twice were the sites reset! This was replaced with a cleanup-logic in the deployment process.
        /// </summary>
        public void FastCgiRemove()
        {
            var phpRuntime = this.GetFastCgiExe();

            IntPtr wow64Value = IntPtr.Zero;

            Wow64DisableWow64FsRedirection(ref wow64Value);

            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%windir%\System32\inetsrv\config\applicationHost.config"));
            XDocument cfgFile = XDocument.Parse(File.ReadAllText(path));

            // Remove this app
            var apps = (from p in cfgFile.DescendantsExtended("application")
                        where p.Attribute("fullPath") != null
                              && p.Attribute("fullPath")?.Value == phpRuntime
                        select p);

            foreach (var a in apps.ToList())
            {
                a.Remove();
            }

            // Remove any old paths (cleanup for old bad implementation...)
            // This is importante because many times the removal fails
            // due to applicationHosts.config being in use (bug in IIS)
            // https://forums.iis.net/t/1190387.aspx
            apps = (from p in cfgFile.DescendantsExtended("application")
                    where p.Attribute("arguments") != null
                    && p.Attribute("arguments").Value.StartsWith(
                        "chf_" + this.Deployment.installedApplicationSettings.GetId() + "_")
                    select p);

            foreach (var a in apps.ToList())
            {
                string apppath = a.Attribute("fullPath")?.Value;
                if (!File.Exists(apppath))
                {
                    a.Remove();
                }
            }

            Encoding utf8WithoutBom = new UTF8Encoding(false);

            try
            {
                File.WriteAllText(path, cfgFile.ToString(), utf8WithoutBom);
            }
            catch (Exception e)
            {
                // This is a specific type of exception we can ignore.
                if (Convert.ToString((uint)e.HResult) == "2148734208" ||
                    Convert.ToString((uint)e.HResult) == "2147942432")
                {
                    this.Logger.LogWarning(
                        false,
                        "Could not clean up applicationHosts file at '" + path + "'. https://forums.iis.net/t/1190387.aspx.");
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                Wow64RevertWow64FsRedirection(wow64Value);
            }
        }

        public void sync()
        {
        }
    }
}
