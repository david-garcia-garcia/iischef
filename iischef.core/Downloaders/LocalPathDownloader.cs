using iischef.core.SystemConfiguration;
using iischef.logger;
using iischef.utils;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace iischef.core.Downloaders
{
    /// <summary>
    /// Downlader to be used for projects
    /// that exist in the local filesystem
    /// usually development checkouts.
    /// </summary>
    public class LocalPathDownloader : IDownloaderInterface
    {
        /// <summary>
        /// Downloader settings.
        /// </summary>
        protected LocalPathDownloaderSettings Settings;

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
        /// <param name="settings"></param>
        /// <param name="globalSettings"></param>
        /// <param name="logger"></param>
        public LocalPathDownloader(
            LocalPathDownloaderSettings settings,
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
            // We won't limit build id's for local path downloader during
            // testing process.
            var istest = this.GlobalSettings.options.Contains("testenvironment");

            if (!string.IsNullOrWhiteSpace(buildId) && !istest)
            {
                throw new Exception("LocalPathDownloader does not support deploying from a specific buildId.");
            }

            if (this.Settings.monitorChangesTo != null && this.Settings.monitorChangesTo.Any())
            {
                StringBuilder signature = new StringBuilder();

                var difo = new DirectoryInfo(this.Settings.path);

                foreach (var p in this.Settings.monitorChangesTo)
                {
                    var files = difo.EnumerateFiles(p);

                    foreach (var f in files)
                    {
                        signature.AppendLine(f.FullName + ":" + f.LastWriteTimeUtc.ToUnixTimestamp());
                    }
                }

                // Si cambia alguno de los ficheros esta firma cambiará.
                return "monitorchanges:" + UtilsEncryption.GetMD5(signature.ToString());
            }

            // Por defecto usa el lastwritetime del directorio... esto en Windows es una mierda
            // porque no hay propagación vertical de esta información (i.e. si modificar un fichero
            // dentro del directorio la fecha del directorio no cambia).
            return "lastwritetime:" + (new DirectoryInfo(this.Settings.path)).LastWriteTimeUtc.ToUnixTimestamp();
        }

        /// <inheritdoc cref="IDownloaderInterface"/>
        public Artifact PullFromId(string id, string preferredArtifactPath)
        {
            Artifact artifact = new Artifact();

            artifact.obtainedAt = DateTime.UtcNow;
            artifact.id = id;
            artifact.localPath = this.Settings.path;

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
