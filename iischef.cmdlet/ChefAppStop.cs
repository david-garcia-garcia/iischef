using iischef.core;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Re-Depoy an application using it's ID, or redeploy ALL app's!
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppStop")]
    [OutputType(typeof(Deployment))]
    public class ChefAppStop : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.StopAppById(this.Id);
            });
        }
    }
}
