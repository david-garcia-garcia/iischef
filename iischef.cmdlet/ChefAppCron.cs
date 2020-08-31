using iischef.core;
using iischef.core.Configuration;
using System.Collections.Generic;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Sometimes deployements are tough (specially on IIS)
    /// so we might get cases of "old" stuck websites.
    /// 
    /// Use this to trigger several types of cleanup.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppCron")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppCron : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RunCron(this.Id);
            });
        }
    }
}
