using iischef.core.IIS;
using iischef.logger;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Setup/reconfigure the site and rewrite rules required for resolving Let's encrypt ACME challenges
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefSyncCertsToSite")]
    public class IISChefSyncCertsToSite : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// The certificate store location
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string SiteName { get; set; }

        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);
            SslBindingSync.SyncCcsBindingsToSite(this.SiteName, logger);
        }
    }
}
