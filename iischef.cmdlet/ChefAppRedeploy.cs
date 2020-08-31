using System.Linq;
using System.Management.Automation;
using iischef.core;

namespace iischef.cmdlet
{
    /// <summary>
    /// Re-Depoy an application using it's ID, or redeploy ALL app's!
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRedeploy")]
    [OutputType(typeof(Deployment))]
    public class ChefAppRedeploy : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string BuildId { get; set; }

        [Parameter]
        public SwitchParameter FromTemplate { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Set tags for this deployment
        /// </summary>
        [Parameter]
        public string MergeTags { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                var logger = app.GetLogger();
                var deployment = app.RedeployInstalledApplication(this.FromTemplate, this.Id, this.Force, this.BuildId, tags: this.MergeTags);
                this.WriteObject(deployment);
            });
        }
    }
}
