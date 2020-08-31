using iischef.core;
using System.IO;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Use for "quick" deployment from paths.... used
    /// during testing builds.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppDeployZip")]
    [OutputType(typeof(Deployment))]
    public class ChefAppDeployZip : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Path { get; set; }

        [Parameter(Position = 2, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Id { get; set; }

        /// <summary>
        /// Wether or not to install the app
        /// </summary>
        [Parameter(Position = 3, ValueFromPipelineByPropertyName = true)]
        public SwitchParameter Install { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();

                var configuration = @"id: '{0}'
downloader:
 type: 'localzip'
 path: '{1}'
";
                configuration = string.Format(configuration, this.Id, this.Path);

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
