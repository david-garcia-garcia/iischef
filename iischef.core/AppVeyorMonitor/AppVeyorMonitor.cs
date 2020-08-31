using iischef.logger;
using iischef.utils;
using System;
using System.IO;

namespace iischef.core.AppVeyorMonitor
{
    /// <summary>
    /// Engine used to monitor a remote AppVeyor project
    /// in order to automatically detect publish environments
    /// </summary>
    public class AppVeyorMonitor
    {
        /// <summary>
        /// Connection settings
        /// </summary>
        protected AppVeyorMonitorSettings Settings;

        /// <summary>
        /// The API client.
        /// </summary>
        protected utils.AppVeyor.Client client;

        /// <summary>
        /// The publish pattern
        /// </summary>
        protected string pattern;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// The application itself...
        /// </summary>
        private Application app;

        /// <summary>
        /// Get an instance of AppVeyorMonitor
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="app"></param>
        /// <param name="logger"></param>
        public AppVeyorMonitor(
            AppVeyorMonitorSettings settings,
            Application app,
            ILoggerInterface logger)
        {
            this.Settings = settings;
            this.client = new utils.AppVeyor.Client(this.Settings.apitoken, "https://ci.appveyor.com", logger, app.GetGlobalSettings().GetDefaultTempStorage().path);
            this.app = app;
            this.Logger = logger;
        }

        /// <summary>
        /// Inspects any succesful AppVeyor builds
        /// for a specific commit message that triggers a deploy.
        /// </summary>
        public void FindNewDeployments()
        {
            this.Logger.LogInfo(true, $"Looking for last succesful builds in appveyor project '{this.Settings.project}'");

            // Find all last succesful builds that have a chef message.
            var builds = this.client.FindLastSuccessfulBuilds(
                this.Settings.username,
                this.Settings.project,
                null,
                100,
                null,
                Message.PATTERN,
                50);

            foreach (var build in builds)
            {
                this.ProcessMessage(build);
            }
        }

        /// <summary>
        /// Deploy a single build.
        /// </summary>
        /// <param name="build"></param>
        protected void ProcessMessage(utils.AppVeyor.Build build)
        {
            Message command = new Message(build.message);

            try
            {
                switch (command.command)
                {
                    case "publish":
                    case "republish":
                        this.RunPublishCommand(command, build);
                        break;
                    default:
                        this.Logger.LogWarning(false, "AppVeyor commit message command not found: '{0}'", Newtonsoft.Json.JsonConvert.SerializeObject(command));
                        break;
                }
            }
            catch (Exception ex)
            {
                var newEx = new Exception("Error executing command: " + Newtonsoft.Json.JsonConvert.SerializeObject(command), ex);
                this.Logger.LogException(newEx);
            }
        }

        /// <summary>
        /// Execute the publish/republish commands.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="build"></param>
        protected void RunPublishCommand(Message command, utils.AppVeyor.Build build)
        {
            double lifetime = double.Parse(command.arguments[0]);

            // Limit the lifetime to 7 days
            if (lifetime > 168)
            {
                lifetime = 168;
            }

            // To prevent colision between projects, add a small project hash
            var projectHash = UtilsEncryption.GetShortHash(build.project.name, 2);

            // Use the branch name to generate a unique application name
            var appId = $"{Application.AutoDeployApplicationIdPrefix}{projectHash}_{build.branch}".ToLower();

            // Use an in-code template to deploy
            string template = string.Format(
@"id: '{0}'
expires: {4}
tags: '{6}'
autodeploy: true
downloader:
  type: 'appveyor'
  project: '{1}'
  username: '{2}'
  apitoken: '{5}'
  branch: '{3}'
",
appId,
this.Settings.project,
this.Settings.username,
build.branch,
lifetime,
this.Settings.apitoken,

// Tag to indicate that this is an autodeployment.
"autodeploy");

            // Update the deployment template
            string templateFilePath = Path.Combine(this.app.GetGlobalSettings().applicationTemplateDir, appId + ".yml");
            File.WriteAllText(templateFilePath, template);

            if ("republish".Equals(command.command, StringComparison.CurrentCultureIgnoreCase))
            {
                bool doRemove = true;

                // To prevent infinite redeployment loops
                // we need to make sure that we do not remove
                // the application if currently installed version (if any)
                var application = this.app.GetInstalledApp(appId);

                var buildVersion = new Version("0.0.0");
                var deployedVersion = new Version("0.0.0");

                if (application != null)
                {
                    var deployer = this.app.GetDeployer(application);

                    Version.TryParse(build.version, out buildVersion);
                    Version.TryParse(deployer.DeploymentActive?.artifact?.id, out deployedVersion);

                    doRemove = buildVersion > deployedVersion;
                }

                if (doRemove)
                {
                    this.Logger.LogInfo(false, "Application republish from version '{0}' to '{1}'", deployedVersion, buildVersion);
                    this.app.RemoveAppById(appId, true);
                }
            }

            // Deploy from template...
            this.app.RedeployInstalledApplication(true, appId, false);

            // Get rid of the template, this is not useful anymore.
            File.Delete(templateFilePath);
        }
    }
}
