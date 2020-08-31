using iischef.core.Configuration;
using iischef.core.SystemConfiguration;
using iischef.logger;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iischef.core
{
    /// <summary>
    /// Manage a collection of deployers
    /// </summary>
    public class DeployerCollection : List<IDeployerInterface>
    {
        /// <summary>
        /// The global settings
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        /// <summary>
        /// The deployment
        /// </summary>
        protected Deployment Deployment;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Inherit from
        /// </summary>
        protected InstalledApplication InhertApp;

        protected string Indent = "==> ";

        /// <summary>
        /// Get an instance of DeployerCollection
        /// </summary>
        public DeployerCollection(
                EnvironmentSettings globalSettings,
                Deployment deployment,
                ILoggerInterface logger,
                InstalledApplication inhertApp)
        {
            this.GlobalSettings = globalSettings;
            this.Deployment = deployment;
            this.Logger = logger;
            this.InhertApp = inhertApp;
        }

        /// <summary>
        /// Add an item
        /// </summary>
        /// <param name="deployerType"></param>
        /// <param name="deployerSettings"></param>
        public void AddItem(Type deployerType, JObject deployerSettings)
        {
            IDeployerInterface instance = (IDeployerInterface)System.Activator.CreateInstance(deployerType);
            instance.initialize(this.GlobalSettings, deployerSettings, this.Deployment, this.Logger, this.InhertApp);
            instance.weight = this.Count;
            this.Add(instance);
        }

        /// <summary>
        /// Start all deployers
        /// </summary>
        public void StartAll(bool continueOnError = false)
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(false, $"{this.Indent}Starting '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.start();
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while starting on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Cleanup all deployers
        /// </summary>
        public void CleanupAll(bool continueOnError = false)
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(false, $"{this.Indent}Cleanup '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.cleanup();
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while cleanup on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Cleanup all deployers
        /// </summary>
        public void Cron(bool continueOnError = false)
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(true, $"{this.Indent}Cron '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.cron();
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while stopping on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Stop all deployers
        /// </summary>
        /// <param name="continueOnError"></param>
        public void StopAll(bool continueOnError = false)
        {
            foreach (var p in this.OrderByDescending((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(false, $"{this.Indent}Stopping '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.stop();
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while stopping on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Deploy all
        /// </summary>
        public void DeployAll()
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                this.Logger.LogInfo(false, $"{this.Indent}Deploying '{p.GetType().Name}' on {this.Deployment.shortid}");
                p.deploy();
            }
        }

        /// <summary>
        /// Sync all
        /// </summary>
        public void SyncAll()
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                this.Logger.LogInfo(false, $"{this.Indent}Sync '{p.GetType().Name}' on {this.Deployment.shortid}");
                p.sync();
            }
        }

        /// <summary>
        /// Deploy application settings.
        /// </summary>
        /// <param name="jsonSettings"></param>
        /// <param name="jsonSettingsNested"></param>
        public void DeploySettingsAll(string jsonSettings, string jsonSettingsNested)
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                p.deploySettings(jsonSettings, jsonSettingsNested, this.Deployment.GetSettingsReplacer());
            }
        }

        /// <summary>
        /// Undeploy all
        /// </summary>
        /// <param name="continueOnError"></param>
        /// <param name="isUninstall"></param>
        public void UndeployAll(bool continueOnError = false, bool isUninstall = false)
        {
            // Undeployment must be done in reverse weight order,
            // because "deployers" might depend on what others have
            // set (such as IIS site needing the folder created by
            // the AppBase storage deployer)
            foreach (var p in this.OrderByDescending((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(false, $"{this.Indent}Undeploy '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.undeploy(isUninstall);
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while undeploying on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Call donde on all deployers
        /// </summary>
        public void BeforeDoneAll()
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                this.Logger.LogInfo(false, $"{this.Indent}BeforeDone '{p.GetType().Name}' on {this.Deployment.shortid}");
                p.beforeDone();
            }
        }

        /// <summary>
        /// Call donde on all deployers
        /// </summary>
        /// <param name="continueOnError"></param>
        public void DoneAll(bool continueOnError = false)
        {
            foreach (var p in this.OrderBy((i) => i.weight))
            {
                try
                {
                    this.Logger.LogInfo(false, $"{this.Indent}Done '{p.GetType().Name}' on {this.Deployment.shortid}");
                    p.done();
                }
                catch (Exception e)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    this.Logger.LogException(new Exception("Silent exception while calling done on " + this.Deployment.shortid, e), EventLogEntryType.Warning);
                }
            }
        }
    }
}
