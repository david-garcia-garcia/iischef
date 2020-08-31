using iischef.core;
using iischef.utils;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefAppRemoveExpired")]
    [OutputType(typeof(List<core.Configuration.InstalledApplication>))]
    public class ChefAppRemoveExpired : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Id { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var app = ConsoleUtils.GetApplicationForConsole();
                app.RemoveExpiredApplications(DateTime.UtcNow.ToUnixTimestamp(), this.Id);
            });
        }
    }
}
