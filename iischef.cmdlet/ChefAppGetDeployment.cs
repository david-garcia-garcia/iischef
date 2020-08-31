using iischef.core;
using System.Linq;
using System.Management.Automation;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppGetDeployment")]
    [OutputType(typeof(ApplicationDeployer))]
    public class ChefAppGetDeployment : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            var app = ConsoleUtils.GetApplicationForConsole();
            var installedApplication = app.GetInstalledApps(this.Id).Single();
            var deployer = app.GetDeployer(installedApplication);

            this.WriteObject(deployer);
        }
    }
}
