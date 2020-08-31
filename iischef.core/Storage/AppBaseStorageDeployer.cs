using iischef.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace iischef.core.Storage
{
    public class AppBaseStorageDeployer : DeployerBase, IDeployerInterface
    {
        public const string LEGACY_CHEF_USERS_GROUPNAME = "chef_users";

        /// <summary>
        /// List of configuration files were the runtime
        /// settings will be replaced.
        /// </summary>
        /// <returns></returns>
        public AppBaseStorageDeployerSettings GetSettings()
        {
            return (AppBaseStorageDeployerSettings)this.DeployerSettings.ToObject(typeof(AppBaseStorageDeployerSettings));
        }

        /// <inheritdoc cref="DeployerBase"/>
        public void deploy()
        {
            var settings = this.GetSettings();

            this.Deployment.windowsUsername = "chf_" + this.Deployment.installedApplicationSettings.GetId();

            if (this.Deployment.GetPreviousDeployment() != null && this.Deployment.GetPreviousDeployment().windowsUsername != this.Deployment.windowsUsername)
            {
                this.Logger.LogWarning(
                    false,
                    "Windows account username has changed from '{0}' to '{1}', removal of account and granted permissions must be performed manually.",
                    this.Deployment.GetPreviousDeployment()?.windowsUsername,
                    this.Deployment.windowsUsername);
            }

            UtilsWindowsAccounts.EnsureUserExists(this.Deployment.WindowsUsernameFqdn(), this.Deployment.GetWindowsPassword(), this.Deployment.installedApplicationSettings.GetId(), this.Logger, this.GlobalSettings.directoryPrincipal);

            // Legacy behaviour, if no userGroups defined, create a chef_users groups and add the users
            // to it
            if (!(this.GlobalSettings.userGroups ?? new List<string>()).Any())
            {
                UtilsWindowsAccounts.EnsureGroupExists(LEGACY_CHEF_USERS_GROUPNAME, this.GlobalSettings.directoryPrincipal);
                UtilsWindowsAccounts.EnsureUserInGroup(this.Deployment.WindowsUsernameFqdn(), LEGACY_CHEF_USERS_GROUPNAME, this.Logger, this.GlobalSettings.directoryPrincipal);
            }

            // Add the user to the user groups
            foreach (var groupIdentifier in this.GlobalSettings.userGroups ?? new List<string>())
            {
                UtilsWindowsAccounts.EnsureUserInGroup(this.Deployment.WindowsUsernameFqdn(), groupIdentifier, this.Logger, this.GlobalSettings.directoryPrincipal);
            }

            // Add the user to any user groups defined at the application level
            foreach (var groupIdentifier in settings.user_groups ?? new List<string>())
            {
                UtilsWindowsAccounts.EnsureUserInGroup(this.Deployment.WindowsUsernameFqdn(), groupIdentifier, this.Logger, this.GlobalSettings.directoryPrincipal);
            }

            // Add any privileges if requested
            foreach (var privilegeName in settings.privileges ?? new List<string>())
            {
                UtilsWindowsAccounts.SetRight(this.Deployment.WindowsUsernameFqdn(), privilegeName, this.Logger);
            }

            // Getting security right at the OS level here is a little bit picky...
            // in order to have REALPATH to work in PHP we need to be able to read all directories
            // in a path i.e. D:\webs\chef\appnumber1\
            // What we will do is disconnect the USERS account here...
            string basePath = UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultApplicationStorage().path, this.Deployment.getShortId());
            UtilsSystem.EnsureDirectoryExists(basePath, true);

            UtilsWindowsAccounts.DisablePermissionInheritance(basePath);
            UtilsWindowsAccounts.RemoveAccessRulesForIdentity(new SecurityIdentifier(UtilsWindowsAccounts.WELL_KNOWN_SID_USERS), basePath, this.Logger);
            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), basePath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);

            // Store this in the application storage location.
            this.Deployment.runtimePath = UtilsSystem.CombinePaths(basePath, "runtime");
            UtilsSystem.EnsureDirectoryExists(this.Deployment.runtimePath, true);

            this.Deployment.runtimePathWritable = UtilsSystem.CombinePaths(basePath, "runtime_writable");
            UtilsSystem.EnsureDirectoryExists(this.Deployment.runtimePathWritable, true);

            // Due to compatibility reasons with environments such as PHP (that do not play well with network file URIs such as shared folders)
            // by default these two directories are symlinked to a local path if they are network paths.

            // Temp dir
            string localTempPath = UtilsSystem.CombinePaths(this.Deployment.runtimePath, "temp");
            string remoteTempPath = UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultTempStorage().path, this.Deployment.installedApplicationSettings.GetId());
            UtilsSystem.EnsureDirectoryExists(remoteTempPath, true);
            UtilsJunction.EnsureLink(localTempPath, remoteTempPath, this.Logger, false);
            this.Deployment.tempPath = localTempPath;

            // Temp dir sys
            this.Deployment.tempPathSys = UtilsSystem.CombinePaths(this.Deployment.runtimePathWritable, "_tmp"); 
            UtilsSystem.EnsureDirectoryExists(this.Deployment.tempPathSys, true);

            // Log dir
            string localLogPath = UtilsSystem.CombinePaths(this.Deployment.runtimePath, "log");
            string remoteLogPath = UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultLogStorage().path, this.Deployment.installedApplicationSettings.GetId());
            UtilsSystem.EnsureDirectoryExists(remoteLogPath, true);
            UtilsJunction.EnsureLink(localLogPath, remoteLogPath, this.Logger, false);
            this.Deployment.logPath = localLogPath;

            this.Deployment.SetSetting("appstorage.base", basePath);
            this.Deployment.SetSetting("appstorage.temp", this.Deployment.tempPath);
            this.Deployment.SetSetting("appstorage.log", this.Deployment.logPath);

            this.Deployment.SetSetting("appstorage.remote_temp", remoteTempPath);
            this.Deployment.SetSetting("appstorage.remote_log", remoteLogPath);

            // We use this flag to detect transient storage
            // that must be removed when the deployer is "undeployed".
            AppBaseStorageType appBaseStorageType = AppBaseStorageType.Original;

            // TODO: Make this configurable through the chef.yml settings file.
            string ignoreOnDeployPattern = "^\\.git\\\\|^chef\\\\|^\\.vs\\\\";

            switch (this.Deployment.installedApplicationSettings.GetApplicationMountStrategy())
            {
                case ApplicationMountStrategy.Copy:
                    this.Deployment.appPath = UtilsSystem.CombinePaths(basePath, "app");
                    
                    // TODO: We should consider the ability to symlink the code here, or to point/mount directly
                    // to the original source path. This would probably require delegating this step to the artifact downloader
                    // (artifact.getDownloader()) or having the downloader tell us how to deal with this (symlinks, direct, whatever)
                    this.Logger.LogInfo(true, "Copying artifact files...");
                    UtilsSystem.CopyFilesRecursivelyFast(this.Deployment.artifact.localPath, this.Deployment.appPath, false, ignoreOnDeployPattern, this.Logger);
                    this.Logger.LogInfo(true, "Ensure app has proper user permissions for account '{0}'", this.Deployment.WindowsUsernameFqdn());
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUserPrincipalName(), this.Deployment.appPath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);
                    this.Deployment.artifact.DeleteIfRemote(this.Logger);
                    appBaseStorageType = AppBaseStorageType.Transient;
                    break;
                case ApplicationMountStrategy.Move:
                    this.Deployment.appPath = UtilsSystem.CombinePaths(basePath, "app");
                    
                    // TODO: We should consider the ability to symlink the code here, or to point/mount directly
                    // to the original source path. This would probably require delegating this step to the artifact downloader
                    // (artifact.getDownloader()) or having the downloader tell us how to deal with this (symlinks, direct, whatever)
                    this.Logger.LogInfo(true, "Moving artifact files...");
                    UtilsSystem.MoveDirectory(this.Deployment.artifact.localPath, this.Deployment.appPath, this.Logger, ignoreOnDeployPattern);
                    
                    // We had issues in appveyor where _webs location is in C drive and thus not giving
                    // permissions here would make tests fail.
                    this.Logger.LogInfo(true, "Ensure app has proper user permissions for account '{0}'", this.Deployment.WindowsUsernameFqdn());
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), this.Deployment.appPath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);
                    this.Deployment.artifact.DeleteIfRemote(this.Logger);
                    appBaseStorageType = AppBaseStorageType.Transient;
                    break;
                case ApplicationMountStrategy.Link:
                    this.Logger.LogInfo(true, "Linking artifact files...");
                    this.Deployment.appPath = UtilsSystem.CombinePaths(basePath, "app");
                    UtilsJunction.EnsureLink(this.Deployment.appPath, this.Deployment.artifact.localPath, this.Logger, false);
                    this.Logger.LogInfo(true, "Ensure app has proper user permissions for account '{0}'", this.Deployment.WindowsUsernameFqdn());
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), this.Deployment.artifact.localPath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);
                    appBaseStorageType = AppBaseStorageType.Symlink;
                    break;
                case ApplicationMountStrategy.Original:
                    this.Logger.LogInfo(true, "Ensure app has proper user permissions for account '{0}'", this.Deployment.WindowsUsernameFqdn());
                    UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), this.Deployment.artifact.localPath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);
                    this.Deployment.appPath = UtilsSystem.CombinePaths(this.Deployment.artifact.localPath);
                    appBaseStorageType = AppBaseStorageType.Original;
                    break;
                default:
                    throw new NotImplementedException("The requested mount strategy for the application is not available: " + this.Deployment.installedApplicationSettings.GetApplicationMountStrategy());
            }

            this.Deployment.SetRuntimeSetting("deployment.appPath", this.Deployment.appPath);
            this.Deployment.SetRuntimeSetting("deployment.logPath", this.Deployment.logPath);
            this.Deployment.SetRuntimeSetting("deployment.tempPath", this.Deployment.tempPath);

            this.Deployment.SetSetting("appstorage.appBaseStorageType", appBaseStorageType);

            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), remoteTempPath, FileSystemRights.Write | FileSystemRights.Read | FileSystemRights.Delete, this.GlobalSettings.directoryPrincipal);
            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), remoteLogPath, FileSystemRights.Write | FileSystemRights.Read | FileSystemRights.Delete, this.GlobalSettings.directoryPrincipal);
            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), this.Deployment.runtimePath, FileSystemRights.ReadAndExecute, this.GlobalSettings.directoryPrincipal);
            UtilsWindowsAccounts.AddPermissionToDirectoryIfMissing(this.Deployment.WindowsUsernameFqdn(), this.Deployment.runtimePathWritable, FileSystemRights.Write | FileSystemRights.Read | FileSystemRights.Delete, this.GlobalSettings.directoryPrincipal);

            this.DeployFonts(settings);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        protected void DeployFonts(AppBaseStorageDeployerSettings settings)
        {
            // Nada que desplegar
            if (settings.fonts?.Any() != true)
            {
                return;
            }

            // TODO: Newer windows versions allow for per-user font registration, we should implement that
            // because with this approach fonts are installed first come first-served, and cannot be updated
            // if necessary. Plus we don't know who owns a font, se we can't delete them on cleanup

            var utilsFont = new UtilsFont();

            foreach (var font in settings.fonts.AsIterable())
            {
                // Check that the provided path is a zip with the font
                var fontSourceZip = Path.Combine(this.Deployment.appPath, font.Path);

                if (Path.GetExtension(fontSourceZip) != ".zip")
                {
                    throw new Exception($"Unable to install the font '{font.Path}' because fonts have to be in a .zip file.");
                }

                if (!File.Exists(fontSourceZip))
                {
                    throw new Exception($"Font file not found '{font.Path}'.");
                }

                // We use a know directory
                var fontTempPath = UtilsSystem.GetTempPath("font-" + Guid.NewGuid());
                var fontPersitentTempPath = UtilsSystem.GetTempPath("font-chef");

                try
                {
                    ZipFile.ExtractToDirectory(fontSourceZip, fontTempPath);
                    UtilsSystem.CopyFilesRecursively(new DirectoryInfo(fontTempPath), new DirectoryInfo(fontPersitentTempPath), true, true);
                    utilsFont.InstallFont(fontPersitentTempPath);
                }
                finally
                {
                    UtilsSystem.DeleteDirectory(fontTempPath, this.Logger, 8);

                    // This will some fonts get locked by the native API without explanation
                    try
                    {
                        UtilsSystem.DeleteDirectory(fontPersitentTempPath, this.Logger, 4);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        public void undeploy(bool isUninstall = false)
        {
            if (this.Deployment == null)
            {
                return;
            }

            var strategy = this.Deployment.installedApplicationSettings.GetApplicationMountStrategy();
            string basePath = UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultApplicationStorage().path, this.Deployment.getShortId());

            switch (strategy)
            {
                case ApplicationMountStrategy.Copy:
                case ApplicationMountStrategy.Move:
                    UtilsSystem.DeleteDirectoryAndCloseProcesses(basePath, this.Logger, UtilsSystem.DefaultProcessWhitelist, 100);
                    break;
                case ApplicationMountStrategy.Link:
                    UtilsJunction.RemoveJunction(this.Deployment.appPath);
                    UtilsSystem.DeleteDirectoryAndCloseProcesses(basePath, this.Logger, UtilsSystem.DefaultProcessWhitelist, 100);
                    break;
                case ApplicationMountStrategy.Original:
                    // Do nothing!
                    break;
                default:
                    throw new Exception("Option not supported.");
            }

            if (!isUninstall)
            {
                return;
            }

            var canonicalPath = this.Deployment.GetSetting<string>("appstorage.canonical", null, this.Logger);
            if (Directory.Exists(canonicalPath) && UtilsJunction.IsJunctionOrSymlink(canonicalPath))
            {
                Directory.Delete(canonicalPath);
            }

            // Usually the IIS site has been closed a few fractions of a second
            // before this is called, so the folders have probably not yet
            // been released, waitPauseMs at least 10 seconds.
            UtilsSystem.DeleteDirectoryAndCloseProcesses(this.Deployment.GetSetting<string>("appstorage.temp", null, this.Logger), this.Logger, UtilsSystem.DefaultProcessWhitelist, 60);
            UtilsSystem.DeleteDirectoryAndCloseProcesses(this.Deployment.GetSetting<string>("appstorage.log", null, this.Logger), this.Logger, UtilsSystem.DefaultProcessWhitelist, 60);
            UtilsSystem.DeleteDirectoryAndCloseProcesses(this.Deployment.GetSetting<string>("appstorage.remote_temp", null, this.Logger), this.Logger, UtilsSystem.DefaultProcessWhitelist, 60);
            UtilsSystem.DeleteDirectoryAndCloseProcesses(this.Deployment.GetSetting<string>("appstorage.remote_log", null, this.Logger), this.Logger, UtilsSystem.DefaultProcessWhitelist, 60);

            var settings = this.GetSettings();

            var groups = this.GlobalSettings.userGroups ?? new List<string>();

            // add legacy group chef_users
            groups.Add(LEGACY_CHEF_USERS_GROUPNAME);

            // Remove user from all groups before deleting
            foreach (var groupIdentifier in groups)
            {
                UtilsWindowsAccounts.EnsureUserNotInGroup(this.Deployment.WindowsUsernameFqdn(), groupIdentifier, this.Logger, this.GlobalSettings.directoryPrincipal);
            }

            // Add the user to any user groups defined at the application level
            foreach (var groupIdentifier in settings.user_groups ?? new List<string>())
            {
                UtilsWindowsAccounts.EnsureUserNotInGroup(this.Deployment.WindowsUsernameFqdn(), groupIdentifier, this.Logger, this.GlobalSettings.directoryPrincipal);
            }

            UtilsWindowsAccounts.DeleteUser(this.Deployment.WindowsUsernameFqdn(), this.GlobalSettings.directoryPrincipal);
        }

        /// <summary>
        /// Deploy the application runtime settings.
        /// </summary>
        /// <param name="jsonSettings"></param>
        /// <param name="jsonSettingsArray"></param>
        /// <param name="replacer"></param>
        public void deploySettings(
            string jsonSettings,
            string jsonSettingsArray,
            RuntimeSettingsReplacer replacer)
        {
            // Write the settings in a directory in the application folder itself.
            // When deployed as web app or similar, the other deployers must hide this directory...
            var settingsFile = UtilsSystem.EnsureDirectoryExists(
                Path.Combine(this.Deployment.runtimePath, "chef-settings.json"));

            File.WriteAllText(settingsFile, jsonSettings);

            var settingsFileNested = UtilsSystem.EnsureDirectoryExists(
                Path.Combine(this.Deployment.runtimePath, "chef-settings-nested.json"));

            File.WriteAllText(settingsFileNested, jsonSettingsArray);

            // Why don't we write the settings directly to the AppRoot? Because it might
            // be exposed to the public if the application is mounted as a web application...
            // So we just hint to the location of the runtime, and the application
            // must implement the code needed to load the settings.
            var hintFile = UtilsSystem.EnsureDirectoryExists(
                UtilsSystem.CombinePaths(this.Deployment.appPath, "chef-runtime.path"));

            // We hint to the runtime path, not the specific file
            File.WriteAllText(hintFile, this.Deployment.runtimePath);

            // Dump the configuration files if requested to do so...
            foreach (var kvp in this.GetSettings().configuration_dump_paths ?? new Dictionary<string, string>())
            {
                var destinationDir = UtilsSystem.CombinePaths(this.Deployment.appPath, kvp.Value);
                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                var settingsFileDump = UtilsSystem.EnsureDirectoryExists(
                Path.Combine(destinationDir, "chef-settings.json"));

                File.WriteAllText(settingsFileDump, jsonSettings);

                var settingsFileNestedDump = UtilsSystem.EnsureDirectoryExists(
                    Path.Combine(destinationDir, "chef-settings-nested.json"));

                File.WriteAllText(settingsFileNestedDump, jsonSettingsArray);

                var settingsFileNestedYaml = UtilsSystem.EnsureDirectoryExists(
                    Path.Combine(destinationDir, "chef-settings-nested.yml"));

                File.WriteAllText(settingsFileNestedYaml, UtilsYaml.JsonToYaml(jsonSettingsArray));
            }

            // Now replace the settings in the configuration templates
            foreach (var kvp in this.GetSettings().configuration_replacement_files ?? new Dictionary<string, string>())
            {
                var sourcePath = UtilsSystem.CombinePaths(this.Deployment.appPath, kvp.Key);
                var destinationPath = UtilsSystem.CombinePaths(this.Deployment.appPath, kvp.Value);

                var contents = File.ReadAllText(sourcePath);

                if (destinationPath == sourcePath)
                {
                    throw new Exception("Destination and source for configuration settings replacements cannot be the same.");
                }

                contents = replacer.DoReplace(contents);

                File.WriteAllText(destinationPath, contents);
            }
        }

        public override void beforeDone()
        {
            // We also have a canonical access to the deployed app through a symlink
            string basePath = this.Deployment.GetSetting("appstorage.base", (string)null, this.Logger);
            string canonicalPath = UtilsSystem.CombinePaths(this.GlobalSettings.GetDefaultApplicationStorage().path, "_" + this.Deployment.installedApplicationSettings.GetId());
            UtilsJunction.EnsureLink(canonicalPath, basePath, this.Logger, false, true);
            this.Deployment.SetSetting("appstorage.canonical", canonicalPath);
        }

        public void start()
        {
        }

        public void stop()
        {
            if (this.Deployment == null)
            {
                return;
            }
        }

        public void sync()
        {
        }

        public override void done()
        {
            this.Logger.LogInfo(true, "Clearing File System Cache..." + Environment.NewLine + Environment.NewLine + UtilsSystem.DebugTable(FileSystemCache.GetFileSystemCacheBytes()));

            FileSystemCache.ClearFileSystemCache(true);

            this.Logger.LogInfo(true, "Finished clearing File System Cache..." + Environment.NewLine + Environment.NewLine + UtilsSystem.DebugTable(FileSystemCache.GetFileSystemCacheBytes()));
        }
    }
}
