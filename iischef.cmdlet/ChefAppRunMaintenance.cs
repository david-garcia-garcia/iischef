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
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRunMaintenance")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppRunMaintenance : Cmdlet
    {
        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RunMaintenance();
            });
        }
    }
}
