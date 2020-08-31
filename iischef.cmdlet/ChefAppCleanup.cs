using iischef.core;
using System.Collections.Generic;
using System.Management.Automation;
using iischef.core.Configuration;

namespace iischef.cmdlet
{
    /// <summary>
    /// Sometimes deployements are tough (specially on IIS)
    /// so we might get cases of "old" stuck websites.
    /// 
    /// Use this to trigger several types of cleanup.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppCleanup")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppCleanup : Cmdlet
    {
        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.ExecuteCleanup();
            });
        }
    }
}
