using iischef.core;
using iischef.logger;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Tries to get an SSL certificate from Let's Encrypt for the specified hostname and, optionally, forces a renewal.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefGetCert")]
    public class IISChefGetCert : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// The hostname for which to obtain an SSL certificate.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Hostname { get; set; }

        /// <summary>
        /// If specified, sets the minimum number of days remaining on the certificate before it is renewed.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public int? RenewThresholdDays { get; set; }

        /// <summary>
        /// The registration e-mail
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string RegistrationMail { get; set; }

        /// <summary>
        /// If specified, forces the SSL certificate renewal process even if the renewal threshold is not met or there are renewal errors.
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter DisableDnsValidation { get; set; }

        /// <summary>
        /// Provider to use
        /// </summary>
        [Parameter]
        public CertProvider Provider { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);
            var args = new CmdRenewSslArgs();
            args.CertProvider = this.Provider;
            args.HostName = this.Hostname;
            args.RenewThresholdDays = this.RenewThresholdDays ?? 20;
            args.Force = this.Force;
            args.RegistrationMail = this.RegistrationMail;
            args.DisableDnsValidation = this.DisableDnsValidation.IsPresent;
            new CmdRenewSsl().Run(args, logger);
        }
    }
}
