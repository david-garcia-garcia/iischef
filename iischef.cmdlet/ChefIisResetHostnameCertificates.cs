using iischef.core;
using System.Collections.Generic;
using System.Management.Automation;
using iischef.core.Configuration;
using iischef.utils;

namespace iischef.cmdlet
{
    /// <summary>
    /// Sometimes deployements are tough (specially on IIS)
    /// so we might get cases of "old" stuck websites.
    /// 
    /// Use this to trigger several types of cleanup.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChefIisResetHostnameCertificates")]
    [OutputType(typeof(List<InstalledApplication>))]
    public class ChefIisResetHostnameCertificates : Cmdlet
    {
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Hostname { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                var logger = new logger.ConsoleLogger();
                logger.SetVerbose(true);

                UtilsIis.EnsureCertificateInCentralCertificateStoreIsRebound(this.Hostname, logger);
            });
        }
    }
}
