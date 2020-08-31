using iischef.utils;
using Microsoft.Synchronization;
using Microsoft.Synchronization.Files;
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;

namespace iischef.core.Services
{
    /// <summary>
    /// Servicio de aprovisionamiento de almacenaje
    /// persistente para las aplicaciones
    /// </summary>
    public class DiskService : DeployerBase, IDeployerInterface
    {
        /// <summary>
        /// All disk storage for this application is pointed to this directory.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected string GetStoragePath(DiskServiceSettings settings)
        {
            var storage = this.GlobalSettings.GetDefaultContentStorage();

            // We can have an app_setting configuration
            // to route a whole application to a specific sql server
            string diskTarget;

            if (this.Deployment.installedApplicationSettings.configuration["disktarget"] != null)
            {
                diskTarget = Convert.ToString(this.Deployment.installedApplicationSettings.configuration["disktarget"]);
                this.Logger.LogInfo(true, "Custom disk target: " + diskTarget);

                if (!Directory.Exists(diskTarget))
                {
                    throw new Exception("Invalid custom disk target: " + diskTarget);
                }
            }
            else
            {
                // Generate a unique "virtual disk" (directory) for this application
                diskTarget = UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(
                    storage.path,
                    "store_" + this.Deployment.installedApplicationSettings.GetId()));
            }

            return diskTarget;
        }

        public void deploy()
        {
            var diskSettings = this.DeployerSettings.castTo<DiskServiceSettings>();

            var baseStoragePath = this.GetStoragePath(diskSettings);

            if (diskSettings.mounts == null || !diskSettings.mounts.Any())
            {
                throw new Exception("You must specify at least a mount for a disk service.");
            }

            // Each one of these is to be mounted as a symlink/junction
            foreach (var mount in diskSettings.mounts)
            {
                if (string.IsNullOrWhiteSpace(diskSettings.id))
                {
                    throw new Exception("Disk settings must have an id");
                }

                if (string.IsNullOrWhiteSpace(mount.Value.id))
                {
                    throw new Exception("All mounts in disk configuration must have an id");
                }

                // Expand the local path..
                var mountDestination = UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(baseStoragePath, mount.Value.path), true);
                this.Logger.LogInfo(true, "Mounting disk '{0}' at {1}", mount.Value.id, mountDestination);

                var settingkey = $"services.{diskSettings.id}.mount.{mount.Value.id}.path";

                // We might sometimes need to force a specific path in an environment...
                if (this.Deployment.installedApplicationSettings.GetRuntimeSettingsOverrides().ContainsKey(settingkey))
                {
                    string newMountDestination = this.Deployment.installedApplicationSettings.GetRuntimeSettingsOverrides()[settingkey];
                    if (Directory.Exists(newMountDestination))
                    {
                        this.Logger.LogInfo(false, "Default mount for '{0}' overriden with '{1}' from a default value of '{2}'.", settingkey, newMountDestination, mountDestination);
                        mountDestination = newMountDestination;
                    }
                    else
                    {
                        this.Logger.LogInfo(false, "Tried to override mount path ({0}) with a non-existent directory: '{1}'", settingkey, newMountDestination);
                    }
                }

                // Ensure proper permissions
                this.Logger.LogInfo(true, "Ensure mount has proper user permissions for account '{0}'", this.Deployment.WindowsUsernameFqdn());
                UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), mountDestination, FileSystemRights.Modify, this.GlobalSettings.directoryPrincipal);

                string mountPath = null;

                if (!string.IsNullOrWhiteSpace(mount.Value.mountpath))
                {
                    mountPath = UtilsSystem.CombinePaths(this.Deployment.appPath, mount.Value.mountpath);
                    UtilsJunction.EnsureLink(mountPath, mountDestination, this.Logger, mount.Value.persist_on_deploy);
                }

                // Wether we requested or not a mountpath, make a link in the runtime folder to all disk stores
                var localMountPath = UtilsSystem.CombinePaths(this.Deployment.runtimePath, "disk",  mount.Value.id);
                this.Logger.LogInfo(true, "Linking disk at local path {0}", localMountPath);
                UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.Deployment.runtimePath, "disk"), true);
                UtilsJunction.EnsureLink(localMountPath, mountDestination, this.Logger, mount.Value.persist_on_deploy);

                // Make only the local mount path visible to the application
                this.Deployment.SetRuntimeSetting(settingkey, localMountPath);

                this.Deployment.SetSettingCollection($"service.{diskSettings.id}", settingkey, new DiskStore()
                {
                    path = localMountPath,
                    junction = mountPath,
                    originalPath = mountDestination,
                    junctionRealPath = UtilsJunction.ResolvePath(mountPath)
                });
            }
        }

        public void sync()
        {
            base.syncCommon<DiskService>();
        }

        public override void _sync(object input)
        {
            DiskService other = (DiskService)input;
            var diskSettings = this.DeployerSettings.castTo<DiskServiceSettings>();
            var storage = this.GlobalSettings.GetDefaultContentStorage();

            // Generate a unique virtual disk for this application
            DiskServiceSettings otherSettings = other.DeployerSettings.castTo<DiskServiceSettings>();
            foreach (var mount in diskSettings.mounts)
            {
                string pathOri = UtilsSystem.EnsureDirectoryExists(other.Deployment.GetRuntimeSettingsToDeploy()["services." + otherSettings.id + ".mount.files.path"]);
                string pathDest = UtilsSystem.EnsureDirectoryExists(this.Deployment.GetRuntimeSettingsToDeploy()["services." + diskSettings.id + ".mount.files.path"]);
                FileSyncProvider ori = new FileSyncProvider(pathOri);
                FileSyncProvider dest = new FileSyncProvider(pathDest);

                SyncOrchestrator agent = new SyncOrchestrator();
                agent.LocalProvider = ori;
                agent.RemoteProvider = dest;
                agent.Direction = SyncDirectionOrder.Upload;

                SyncOperationStatistics syncStats = agent.Synchronize();
                this.Logger.LogInfo(
                    true, 
                    "Synchronization stats \n\n local provider {0} to remote {1}\n upload changes applied {2}\n {3} upload changes failed",
                    pathOri,
                    pathDest,
                    syncStats.UploadChangesApplied,
                    syncStats.UploadChangesFailed);
                ori.Dispose();
                dest.Dispose();
            }
        }

        public void undeploy(bool isUninstall = false)
        {
            // Not that we want to accidentally delete contents and files
            // for an application...
            if (!isUninstall)
            {
                return;
            }

            var diskSettings = this.DeployerSettings.castTo<DiskServiceSettings>();
            var mounts = this.Deployment.GetSettingCollection<DiskStore>($"service.{diskSettings.id}");

            foreach (var m in mounts.Values)
            {
                // Most of the time this directory will not exist, as the base storage deployer will already have
                // deleted the application folder. But for "local" installed applications, this removes
                // the symlinks.
                if (!string.IsNullOrWhiteSpace(m.junctionRealPath) && Directory.Exists(m.junctionRealPath))
                {
                    UtilsJunction.RemoveJunction(m.junctionRealPath);
                }

                if (Directory.Exists(m.path))
                {
                    UtilsSystem.DeleteDirectory(m.path, this.Logger);
                }
            }

            var baseStoragePath = this.GetStoragePath(diskSettings);
            if (!string.IsNullOrWhiteSpace(baseStoragePath) && Directory.Exists(baseStoragePath))
            {
                UtilsSystem.DeleteDirectory(baseStoragePath, this.Logger);
            }
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
    }
}
