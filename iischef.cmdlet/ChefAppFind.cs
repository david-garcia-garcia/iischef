using iischef.core;
using iischef.core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Console = System.Console;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppFind")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefAppFind : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            var app = ConsoleUtils.GetApplicationForConsole();

            var apps = app.GetInstalledApps();
            apps = apps.OrderBy((i) => i.GetId()).ToList();

            if (this.Id == null)
            {
                this.WriteObject(apps);

                foreach (var a in apps)
                {
                    Console.WriteLine(a.GetId());
                }

                return;
            }

            foreach (var a in apps)
            {
                if (a.GetId() == this.Id)
                {
                    this.WriteObject(a);
                    return;
                }
            }

            throw new Exception("Application not found: " + this.Id);
        }
    }
}
