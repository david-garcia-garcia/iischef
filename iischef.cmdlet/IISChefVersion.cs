using iischef.core;
using iischef.logger;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Setup/reconfigure the site and rewrite rules required for resolving Let's encrypt ACME challenges
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefVersion")]
    public class IISChefVersion : ChefCmdletBase
    {
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            this.WriteObject($"Running version {BuildVersion.BUILDVERSION}");
        }
    }
}
