using iischef.core.IIS;
using iischef.logger;

namespace iischef.core
{
    public class CmdRenewSsl
    {
        public void Run(CmdRenewSslArgs args, ILoggerInterface logger)
        {
            var provider = new SslCertificateProviderService(logger, args);

            provider.ProvisionCertificateInIis(
                args.HostName,
                args.RegistrationMail,
                force:
                args.Force,
                args.UseSelfSignedFallbackIfNoPfx);
        }
    }
}
