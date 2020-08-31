using iischef.core;
using System.Management.Automation;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppDeploy")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Important Class")]
    public class CheafAppDeploy : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Path { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.DeploySingleAppFromFile(this.Path, true);
            });
        }
    }
}
