using System.Linq;
using System.Management.Automation;
using iischef.core;

namespace iischef.cmdlet
{
    /// <summary>
    /// Re-Depoy an application using it's ID, or redeploy ALL app's!
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRestart")]
    [OutputType(typeof(Deployment))]
    public class ChefAppRestart : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RestartAppById(this.Id);
            });
        }
    }
}
