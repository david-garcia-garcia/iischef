using System;
using System.Collections.Generic;
using iischef.core;
using iischef.core.IIS;
using iischef.core.SystemConfiguration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace healthmonitortests.IIS
{
    [TestClass]
    public class ACMEProvider
    {
        [TestMethod]
        public void ProvisionCertificateInIISV2Test()
        {
            var logger = new TestLogsLogger(this, nameof(this.ProvisionCertificateInIISV2Test));
            EnvironmentSettings settings = new EnvironmentSettings();
            settings.contentStorages = new List<StorageLocation>();
            settings.primaryContentStorage = "01";
            StorageLocation sl = new StorageLocation();
            sl.id = "01";
            sl.path = @"C:\temp";
            sl.type = "String";
            settings.contentStorages.Add(sl);
            settings.AcmeProvider = "certes";

            Deployment deployment = new Deployment()
            {
                windowsUsername = "Administrator",
                globalSettings = settings
            };

            SslCertificateProviderService sslCPS = new SslCertificateProviderService(logger, Guid.NewGuid().ToString(), settings, deployment);

            // string hostname2 = "demogustavo1.sabentishosting.com";
            string hostname = "demogustavo2.sabentishosting.com";
            string email = "info@sabentis.com";
            string bindingInfo = "*:443:demogustavo2.sabentishosting.com"; // infoWithoutSsl
            string ownerSiteName = "Default Web Site";

            sslCPS.ProvisionCertificateInIis(
                hostName: hostname,
                email: email,
                bindingInfo: bindingInfo,
                ownerSiteName: ownerSiteName,
                forceSelfSigned: false,
                forceRenewal: false);
        }
    }
}
