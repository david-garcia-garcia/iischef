using iischef.core.IIS;
using iischef.logger;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Setup/reconfigure the site and rewrite rules required for resolving Let's encrypt ACME challenges
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefSetupAcmeChallenge")]
    public class IISChefSetupAcmeChallenge : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string SharedPath { get; set; }

        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);
            AcmeChallengeSiteSetup acmeChallengeSiteSetup = new AcmeChallengeSiteSetup(logger);
            acmeChallengeSiteSetup.SetupAcmeChallengeSite(this.SharedPath);
        }
    }
}
