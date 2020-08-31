using iischef.utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace iischef.core
{
    public class DeployerBase
    {
        /// <summary>
        /// 
        /// </summary>
        protected SystemConfiguration.EnvironmentSettings GlobalSettings;

        /// <summary>
        /// 
        /// </summary>
        protected Deployment Deployment;

        /// <summary>
        /// 
        /// </summary>
        protected JObject DeployerSettings;

        /// <summary>
        /// 
        /// </summary>
        protected logger.ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        protected Configuration.InstalledApplication ParentApp;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="globalSettings"></param>
        /// <param name="deployerSettings"></param>
        /// <param name="deployment"></param>
        /// <param name="logger"></param>
        /// <param name="inhertApp"></param>
        public virtual void initialize(
                SystemConfiguration.EnvironmentSettings globalSettings,
                JObject deployerSettings,
                Deployment deployment,
                logger.ILoggerInterface logger,
                Configuration.InstalledApplication inhertApp)
        {
            this.Deployment = deployment;
            this.GlobalSettings = globalSettings;
            this.DeployerSettings = deployerSettings;
            this.Logger = logger;
            this.ParentApp = inhertApp;

            if (this.ParentApp != null)
            {
                DeployerSettingsBase settings = deployerSettings.castTo<DeployerSettingsBase>();
            }
        }

        public int weight { get; set; }

        public void deployConsoleEnvironment(StringBuilder command)
        {
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public virtual void done()
        {
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public virtual void beforeDone()
        {
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public virtual void cleanup()
        {
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public virtual void cron()
        {
        }

        /// <summary>
        /// Find the matching deployer of the parent application when
        /// inheritance is configured for this application.
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public TType getDeployerFromParentApp<TType>() 
            where TType : DeployerBase
        {
            // We need a parent application for this to work.
            if (this.ParentApp == null)
            {
                return null;
            }

            // Try to grab parent deployment...
            Deployment parentDeployment;
            string activeDeploymentPathStorage = UtilsSystem.CombinePaths(this.GlobalSettings.activeDeploymentDir, "active." + this.ParentApp.GetId() + ".json");
            if (File.Exists(activeDeploymentPathStorage))
            {
                parentDeployment = Deployment.InstanceFromPath(activeDeploymentPathStorage, this.GlobalSettings);
                DeployerSettingsBase ourSettings = this.DeployerSettings.castTo<DeployerSettingsBase>();
                List<IDeployerInterface> deployersAndServices = new List<IDeployerInterface>();
                deployersAndServices.AddRange(parentDeployment.GrabServices(this.Logger));
                deployersAndServices.AddRange(parentDeployment.GrabDeployers(this.Logger));

                // Only keep those that match our type
                deployersAndServices = deployersAndServices.Where(s => s.GetType() == typeof(TType)).ToList();

                // Filter by ID
                foreach (TType t in deployersAndServices)
                {
                    if (t.DeployerSettings.castTo<DeployerSettingsBase>().id == ourSettings.id)
                    {
                        return t;
                    }
                }

                return null;
            }
            else
            {
                return null;
            }
        }

        public virtual void _sync(object other)
        {
        }

        public void syncCommon<TType>() 
            where TType : DeployerBase
        {
            TType other = this.getDeployerFromParentApp<TType>();

            if (other == null)
            {
                return;
            }

            this._sync(other);
        }
    }
}
