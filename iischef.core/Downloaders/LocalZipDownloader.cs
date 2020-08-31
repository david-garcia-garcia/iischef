using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using System;
using System.IO;
using System.IO.Compression;

namespace iischef.core.Downloaders
{
    /// <summary>
    /// Downlader to be used for projects
    /// that exist in the local filesystem
    /// usually development checkouts.
    /// </summary>
    public class LocalZipDownloader : IDownloaderInterface
    {
        /// <summary>
        /// Downloader settings.
        /// </summary>
        protected LocalZipDownloaderSettings Settings;

        /// <summary>
        /// Global settings.
        /// </summary>
        protected EnvironmentSettings GlobalSettings;

        /// <summary>
        /// The logger service.
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Get an instance of LocalPathDownloader.
        /// </summary>
        public LocalZipDownloader(
            LocalZipDownloaderSettings settings,
            EnvironmentSettings globalSettings,
            ILoggerInterface logger)
        {
            this.Settings = settings;
            this.GlobalSettings = globalSettings;
            this.Logger = logger;
        }

        /// <inheritdoc cref="IDownloaderInterface"/>
        public string GetNextId(string buildId = null)
        {
            return this.Settings.path;
        }

        /// <inheritdoc cref="IDownloaderInterface"/>
        public Artifact PullFromId(string version, string preferredLocalArtifactPath)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                version = this.Settings.path;
            }

            Artifact artifact = new Artifact
            {
                id = version,
                localPath = preferredLocalArtifactPath,
                isRemote = true
            };

            // Use artifact temp path, or local system temporary directory.
            if (Directory.Exists(artifact.localPath))
            {
                UtilsSystem.DeleteDirectory(artifact.localPath, this.Logger);
            }

            // The ID is the PATH to the local zip, so just unzip
            this.Logger.LogInfo(true, "Unzipping file....");
            ZipFile.ExtractToDirectory(version, artifact.localPath);
            this.Logger.LogInfo(true, "Unzipping finished....");

            artifact.obtainedAt = DateTime.UtcNow;

            artifact.artifactSettings = new ArtifactSettings();

            // We will merge data from both git and settings file, local settigns file
            // will override anything from GIT (if available).
            artifact.artifactSettings.PopulateFromGit(artifact.localPath);
            artifact.artifactSettings.PopulateFromSettingsFile(artifact.localPath, this.Logger);
            artifact.artifactSettings.PopulateFromEnvironment();

            // Branch name is critical to some deployment... populate with a no-branch-found....
            if (string.IsNullOrEmpty(artifact.artifactSettings.branch))
            {
                artifact.artifactSettings.branch = "no-branch-found";
                this.Logger.LogInfo(true, "Could not identify git branch for artifact. Using default: 'no-branch-found'");
            }

            return artifact;
        }
    }
}
