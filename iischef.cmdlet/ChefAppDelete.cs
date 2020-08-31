using iischef.core;
using iischef.core.Configuration;
using System.Collections.Generic;
using System.Management.Automation;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRemove")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppDelete : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Id { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RemoveAppById(this.Id, this.Force);
            });
        }
    }
}
