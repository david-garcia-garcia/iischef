using iischef.core;
using System.Collections.Generic;
using System.Management.Automation;
using iischef.core.Configuration;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsCommon.Set, "ChefEnvironmentFile")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class EnvironmentPathSet : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Path { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var logger = new logger.ConsoleLogger();
                logger.SetVerbose(true);
                var app = new core.Application(logger);
                app.UseParentLogger();
                app.SetGlobalEnvironmentFilePath(this.Path);
            });
        }
    }
}
