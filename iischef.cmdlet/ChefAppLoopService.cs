using iischef.core;
using iischef.core.Configuration;
using System.Collections.Generic;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Run an aplication deployment loop
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppLoopService")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppLoopService : Cmdlet
    {
        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RunServiceLoop();
            });
        }
    }
}
