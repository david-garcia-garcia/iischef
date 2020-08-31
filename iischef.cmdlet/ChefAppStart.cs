using iischef.core;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Re-Depoy an application using it's ID, or redeploy ALL app's!
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppStart")]
    [OutputType(typeof(Deployment))]
    public class ChefAppStart : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.StartAppById(this.Id);
            });
        }
    }
}
