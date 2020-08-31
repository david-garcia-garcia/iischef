using iischef.logger;
using iischef.utils;
using System;
using System.IO;
using System.Linq;

namespace iischef.core
{
    public class ArtifactSettings
    {
        /// <summary>
        /// The commit branch.
        /// </summary>
        public string branch { get; set; }

        /// <summary>
        /// The commit hash.
        /// </summary>
        public string commit_sha { get; set; }

        /// <summary>
        /// Build version
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// Try to grab from environment variables (appveyor!!!)
        /// </summary>
        /// <returns></returns>
        public void PopulateFromEnvironment()
        {
            string tempBranch = Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
            if (string.IsNullOrWhiteSpace(this.branch) && !string.IsNullOrWhiteSpace(tempBranch))
            {
                this.branch = tempBranch;
            }

            string tempCommit = Environment.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT");
            if (string.IsNullOrWhiteSpace(this.commit_sha) && !string.IsNullOrWhiteSpace(tempCommit))
            {
                this.commit_sha = tempCommit;
            }

            string tempBuildVersion = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_VERSION");
            if (string.IsNullOrWhiteSpace(this.version) && !string.IsNullOrWhiteSpace(tempBuildVersion))
            {
                this.version = tempBuildVersion;
            }
        }

        /// <summary>
        /// Grab from a settings file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="logger"></param>
        public void PopulateFromSettingsFile(string path, ILoggerInterface logger)
        {
            string file = UtilsSystem.CombinePaths(path, "artifact-settings.yml");

            if (!File.Exists(file))
            {
                return;
            }

            var configfile = new Configuration.YamlConfigurationFile();

            try
            {
                // This file might be malformed, do not crash and let other
                // environment information sources have their chance
                configfile.ParseFromFile(file);
            }
            catch (Exception e)
            {
                logger.LogException(new Exception("Error parsing file: " + file, e));
                return;
            }

            // Parse the artifact settings...
            this.branch = configfile.GetStringValue("repo-branch", null);
            this.commit_sha = configfile.GetStringValue("repo-commit", null);
            this.version = configfile.GetStringValue("build-id", null);
        }

        /// <summary>
        /// Grab from local GIT repo.
        /// </summary>
        /// <param name="path"></param>
        public void PopulateFromGit(string path)
        {
            // Crawl up to find the first directory covered by GIT. There might be a difference
            // between the artifact folder structure and the repository (local working copy) itself...
            DirectoryInfo difo = new DirectoryInfo(path);
            string gitpath = null;
            while (difo != null && difo.Exists)
            {
                if (Directory.Exists(UtilsSystem.CombinePaths(difo.FullName, ".git")))
                {
                    gitpath = difo.FullName;
                    break;
                }

                difo = difo.Parent;
            }

            if (gitpath == null)
            {
                return;
            }

            try
            {
                // Try to get information directly from GIT??
                var repo = new LibGit2Sharp.Repository(gitpath, new LibGit2Sharp.RepositoryOptions() { });

                this.branch = repo.Head.FriendlyName;
                this.commit_sha = repo.Commits.First().Sha;
                this.version = this.commit_sha;
            }
            catch (Exception e)
            {
                // Trying to read settings from GIT can be delicate. Such as...
                // https://github.com/GitTools/GitVersion/issues/1043
            }
        }
    }
}
