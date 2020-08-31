using iischef.utils;

namespace iischef.core.Services
{
    internal class CouchbaseService : DeployerBase, IDeployerInterface
    {
        public void deploy()
        {
            var couchbaseSettings = this.DeployerSettings.castTo<CouchbaseServiceSettings>();

            // There is nothing really here. You simply request
            // an URI, username and password for a bucket. The application
            // is responsible for prefixing it's keys with something
            // unique...
            var couchbaseServer = this.GlobalSettings.GetDefaultCouchbaseServer();

            this.Deployment.SetRuntimeSetting($"services.{couchbaseSettings.id}.uri", couchbaseServer.uri);
            this.Deployment.SetRuntimeSetting($"services.{couchbaseSettings.id}.bucket-name", couchbaseServer.bucketName);
            this.Deployment.SetRuntimeSetting($"services.{couchbaseSettings.id}.bucket-password", couchbaseServer.bucketPassword);
        }

        public void undeploy(bool isUninstall = false)
        {
        }

        public void start()
        {
        }

        public void stop()
        {
        }

        public void deploySettings(
            string jsonSettings,
            string jsonSettingsNested,
            RuntimeSettingsReplacer replacer)
        {
        }

        public void sync()
        {
        }
    }
}
