using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using iischef.utils.AppVeyor;
using System;
using System.IO;
using System.Linq;

namespace iischef.core.Downloaders
{
    /// <summary>
    /// Download artifacts from AppVeyor
    /// </summary>
    public class AppVeyorDownloader : IDownloaderInterface
    {
        protected AppVeyorDownloaderSettings Settings;

        protected Client Client;

        protected ILoggerInterface Logger;

        protected EnvironmentSettings GlobalSettings;

        protected string ApplicationId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="globalSettings"></param>
        /// <param name="logger"></param>
        /// <param name="tempDir">Directory to use for temporary storage.</param>
        /// <param name="applicationId">Application ID, this will be used to customize temp storage paths per application.</param>
        public AppVeyorDownloader(
            AppVeyorDownloaderSettings settings,
            EnvironmentSettings globalSettings,
            ILoggerInterface logger,
            string tempDir,
            string applicationId)
        {
            this.ApplicationId = applicationId;
            this.Settings = settings;
            this.Logger = logger;
            this.GlobalSettings = globalSettings;
            this.Client = new Client(settings.apitoken, "https://ci.appveyor.com", logger, tempDir);
        }

        /// <inheritdoc cref="IDownloaderInterface"/>
        public string GetNextId(string buildId = null)
        {
            // Bring all successful jobs
            var lastBuilds = this.Client.FindLastSuccessfulBuilds(
                this.Settings.username,
                this.Settings.project,
                this.Settings.branch,
                buildVersionRequested: buildId,
                exp: this.Settings.publish_regex_filter,
                maxResults: 2);

            if (!lastBuilds.Any())
            {
                throw new Exception(
                    $"No suitable successful build (buildId={buildId}) found for project {this.Settings.project} on branch {this.Settings.branch}");
            }

            // The build ID uniquely identifies this build.
            return lastBuilds.First().version;
        }

        /// <inheritdoc cref="IDownloaderInterface"/>
        public Artifact PullFromId(string version, string preferredLocalArtifactPath)
        {
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

            // Use the build version to pull the build information.
            Build build = this.Client.GetBuildFromVersion(version, this.Settings.username, this.Settings.project);

            // Make sure that the builds matches the current active branch, otherwise throw an exception
            if (!build.branch.Equals(this.Settings.branch, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new Exception($"Requested version '{version}' with branch '{build.branch}' does not belong to active settings branch '{this.Settings.branch}'");
            }

            this.Client.DownloadSingleArtifactFromBuild(this.ApplicationId, build, this.Settings.artifact_regex, artifact.localPath, this.Logger);

            artifact.artifactSettings = new ArtifactSettings();
            artifact.artifactSettings.PopulateFromSettingsFile(artifact.localPath, this.Logger);

            if (string.IsNullOrWhiteSpace(artifact.artifactSettings.branch))
            {
                artifact.artifactSettings.branch = Convert.ToString(build.branch);
            }

            if (string.IsNullOrWhiteSpace(artifact.artifactSettings.commit_sha))
            {
                artifact.artifactSettings.commit_sha = Convert.ToString(build.commitId);
            }

            return artifact;
        }
    }
}
