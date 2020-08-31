using iischef.core;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Sync an application using it's ID, or sync ALL app's!
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppSync")]
    [OutputType(typeof(Deployment))]
    public class ChefAppSyncId : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                var logger = app.GetLogger();
                var deployment = app.SyncInstalledApplication(this.Id);
                this.WriteObject(deployment);
            });
        }
    }
}
