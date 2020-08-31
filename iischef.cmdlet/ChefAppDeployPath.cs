using iischef.core;
using System.IO;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Use for "quick" deployment from paths.... used
    /// during testing builds.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppDeployPath")]
    [OutputType(typeof(Deployment))]
    public class ChefAppDeployPath : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Path { get; set; }

        [Parameter(Position = 2, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Id { get; set; }

        [Parameter(Position = 3, ValueFromPipelineByPropertyName = true)]
        [ValidateSet("link", "copy", "original")]
        public string MountStrategy { get; set; }

        [Parameter(Position = 4, ValueFromPipelineByPropertyName = true)]
        public string inheritAppId { get; set; }

        /// <summary>
        /// Wether or not to install the app
        /// </summary>
        [Parameter(Position = 5, ValueFromPipelineByPropertyName = true)]
        public SwitchParameter Install { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();

                this.MountStrategy = this.MountStrategy ?? "link";

                var configuration = @"id: '{0}'
mount_strategy: '{2}'
downloader:
 type: 'localpath'
 path: '{1}'
 inherit: '{3}'
";

                configuration = string.Format(configuration, this.Id, this.Path, this.MountStrategy, this.inheritAppId);

                var deployment = app.DeploySingleAppFromTextSettings(configuration, true);

                // If deployment was succesful, write to the installed app folder
                if (this.Install.IsPresent)
                {
                    var destination = app.GetGlobalSettings().applicationTemplateDir + $"\\{this.Id}.yaml";
                    File.WriteAllText(destination, configuration);
                }

                this.WriteObject(deployment);
            });
        }
    }
}
