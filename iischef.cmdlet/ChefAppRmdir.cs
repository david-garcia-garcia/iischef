using iischef.core;
using iischef.core.Configuration;
using System.Collections.Generic;
using System.Management.Automation;
using iischef.utils;

namespace iischef.cmdlet
{
    /// <summary>
    /// Sometimes deployements are tough (specially on IIS)
    /// so we might get cases of "old" stuck websites.
    /// 
    /// Use this to trigger several types of cleanup.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRmdir")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppRmdir : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Directory { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var logger = new logger.ConsoleLogger();
                logger.SetVerbose(true);

                UtilsSystem.DeleteDirectory(this.Directory, logger, 30);
            });
        }
    }
}
