using System;

namespace iischef.core.IIS
{
    [Flags]
    public enum BindingDeploymentMode
    {
        /// <summary>
        /// Deploy the binding, ssl certificates must have been provisioned earlier
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Does not deploy the binding, but prepares ssl certificates
        /// </summary>
        PrepareSsl = 1,

        /// <summary>
        /// If a certificate needs provisioning, it will use a self signed certificate
        /// </summary>
        UseSelfSigned = 2,

        /// <summary>
        /// Force renewal of current certificate, even if renewal conditions are not met
        /// </summary>
        ForceRenewal = 4
    }
}
