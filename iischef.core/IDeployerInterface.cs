using iischef.core.Configuration;
using iischef.core.SystemConfiguration;
using iischef.logger;
using Newtonsoft.Json.Linq;
using System.Text;

namespace iischef.core
{
    public interface IDeployerInterface
    {
        /// <summary>
        /// Initialize the component.
        /// </summary>
        /// <param name="deployerSettings"></param>
        /// <param name="deployment"></param>
        /// <param name="globalSettings"></param>
        /// <param name="logger"></param>
        /// <param name="inhertApp"></param>
        void initialize(
            EnvironmentSettings globalSettings,
            JObject deployerSettings,
            Deployment deployment,
            ILoggerInterface logger,
            InstalledApplication inhertApp);

        /// <summary>
        /// Do any deployment work. Any hot services must be deployed
        /// in a "stopped" state.
        /// </summary>
        void deploy();

        /// <summary>
        /// Remove any deployment data.
        /// </summary>
        /// <param name="isUninstall">For persistent services (syuch as disk storage, databases, etc..) if we should delete the data.</param>
        void undeploy(bool isUninstall = false);

        /// <summary>
        /// Start hot services.
        /// </summary>
        void start();

        /// <summary>
        /// Stop hot services.
        /// </summary>
        void stop();

        /// <summary>
        /// Execution weight/order. Higher weight means
        /// this is the the last to run during installs,
        /// and the first to run during uninstalls.
        /// </summary>
        int weight { get; set; }

        /// <summary>
        /// Deploy the settings JSON to wherever needed in
        /// the environment variables.
        /// </summary>
        void deploySettings(
            string jsonSettings,
            string jsonSettingsNested,
            RuntimeSettingsReplacer replacer);

        /// <summary>
        /// Write any console based initialization commands.
        /// </summary>
        /// <param name="command"></param>
        void deployConsoleEnvironment(StringBuilder command);

        /// <summary>
        /// Called by the application deployer
        /// when deployment has been done succesfully
        /// </summary>
        void done();

        /// <summary>
        /// Called after previous deployment is stopped, but still in a "reversible" state
        /// </summary>
        void beforeDone();

        /// <summary>
        /// Cleans up system wide unused resources.
        /// 
        /// Use with care.
        /// </summary>
        void cleanup();

        /// <summary>
        /// Regular cron for deployers
        /// </summary>
        void cron();

        /// <summary>
        /// Get the inhert deployment from the application configured as the father
        /// 
        /// </summary>
        TType getDeployerFromParentApp<TType>() 
            where TType : DeployerBase;

        /// <summary>
        /// Syncronize the deployment with inherenced application if configured
        /// </summary>
        void sync();
    }
}
