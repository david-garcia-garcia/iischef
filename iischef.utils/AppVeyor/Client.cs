using iischef.logger;
using iischef.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Exception = System.Exception;

namespace iischef.utils.AppVeyor
{
    /// <summary>
    /// Cliente para trabajar sobre la API de AppVeyor
    /// </summary>
    public class Client
    {
        /// <summary>
        /// </summary>
        protected string Token;

        /// <summary>
        /// Base URI for the API i.e. (https://ci.appveyor.com)
        /// </summary>
        protected string BaseUri;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        protected SimpleStore SimpleStore;

        /// <summary>
        /// 
        /// </summary>
        protected string TempDir;

        /// <summary>
        /// Get an instance of AppVeyorClient
        /// </summary>
        /// <param name="token">API Token</param>
        /// <param name="baseUri">Base URI</param>
        /// <param name="logger"></param>
        /// <param name="tempDir"></param>
        public Client(
            string token,
            string baseUri,
            ILoggerInterface logger,
            string tempDir)
        {
            string apiTempDir =
                UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(tempDir, "_appveyor", "api"), true);

            this.TempDir = tempDir;
            this.Token = token;
            this.Logger = logger;
            this.BaseUri = baseUri;
            this.SimpleStore = new SimpleStore(apiTempDir);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="job"></param>
        /// <param name="build"></param>
        /// <param name="artifactRegex"></param>
        /// <returns></returns>
        protected Artifact FindDefaultArtifactForBuild(
            Job job,
            Build build,
            string artifactRegex)
        {
            var artifacts = this.GetArtifactListFromJob(job);

            if (!artifacts.Any())
            {
                throw new Exception($"Requested job '{job.jobId}' from version '{build.version}' has no artifacts.");
            }

            // Try to find a suitable artifact...
            Artifact artifact = null;

            // Search by regex.
            if (!string.IsNullOrWhiteSpace(artifactRegex))
            {
                foreach (var arf in artifacts)
                {
                    string arfname = (string)arf.fileName;

                    if (Regex.IsMatch(arfname, artifactRegex))
                    {
                        if (artifact != null)
                        {
                            throw new Exception($"Ambiguous artifact match for regex: '{artifactRegex}'.");
                        }

                        artifact = arf;
                    }
                }
            }

            if (artifact != null)
            {
                return artifact;
            }

            // If there is only one artifact use that one.
            if (artifacts.Count() == 1)
            {
                return artifacts.First();
            }

            // Use an artifact whose name defaults to the project's name
            artifact = (from p in artifacts
                        where string.Equals(p.name, build.project.name, StringComparison.InvariantCultureIgnoreCase)
                        select p).FirstOrDefault();

            if (artifact != null)
            {
                return artifact;
            }

            // Now use the file name
            artifact = (from p in artifacts
                        where string.Equals(p.fileName, build.project.name + ".zip", StringComparison.InvariantCultureIgnoreCase)
                        select p).FirstOrDefault();

            if (artifact != null)
            {
                return artifact;
            }

            // Our best bet is to find the artifact with the biggest size...
            artifact = artifacts.OrderByDescending((i) => i.size).Where((i) => string.Equals(i.type, "zip", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

            if (artifact == null)
            {
                throw new Exception($"Could not find suitable artifact. Regex: '{artifactRegex}'.");
            }

            return artifact;
        }

        /// <summary>
        /// Downloads (And extracts) single artifacts from jobs.
        /// </summary>
        /// <param name="applicationId"></param>
        /// <param name="build"></param>
        /// <param name="artifactRegex"></param>
        /// <param name="destinationPath"></param>
        /// <param name="logger"></param>
        public void DownloadSingleArtifactFromBuild(
            string applicationId,
            Build build,
            string artifactRegex,
            string destinationPath,
            ILoggerInterface logger)
        {
            UtilsSystem.EnsureDirectoryExists(destinationPath, true);

            // Use the first job in the build...
            var job = build.jobs.First();
            var artifact = this.FindDefaultArtifactForBuild(job, build, artifactRegex);

            var filename = artifact.fileName;
            var extension = Path.GetExtension(filename);

            string downloadTemporaryDir =
                UtilsSystem.EnsureDirectoryExists(UtilsSystem.CombinePaths(this.TempDir, "_appveyor", "dld", applicationId), true);

            int artifactRetentionNum = 5;
            int artifactAgeHoursForStale = 24;

            // Do not touch the latest artifactRetentionNum artifacts or artifacts that are not older than artifactAgeHoursForStale hours
            var staleFiles = Directory.EnumerateFiles(downloadTemporaryDir)
                .Select((i) => new FileInfo(i))
                .Where((i) => i.Extension.Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending((i) => i.CreationTimeUtc)
                .Skip(artifactRetentionNum)
                .Where((i) => (DateTime.UtcNow - i.LastWriteTime).TotalHours > artifactAgeHoursForStale)
                .ToList();

            foreach (var f in staleFiles)
            {
                // Make this fail proof, it's just a cleanup.
                try
                {
                    this.Logger.LogInfo(true, "Removing stale artifact cache file {0}", f.FullName);
                    f.Delete();
                }
                catch
                {
                    // ignored
                }
            }

            // Use a short hash as the temporary file name, because long paths can have issues...
            var tmpFile = UtilsSystem.CombinePaths(downloadTemporaryDir, UtilsEncryption.GetShortHash(JsonConvert.SerializeObject(build) + filename) + extension);

            if (Path.GetExtension(tmpFile)?.ToLower() != ".zip")
            {
                throw new NotImplementedException("AppVeyor artifacts should only be Zip Files.");
            }

            if (!File.Exists(tmpFile))
            {
                // Use an intermediate .tmp file just in case the files does not finish to download,
                // if it exists, clear it.
                string tmpFileDownload = tmpFile + ".tmp";
                if (File.Exists(tmpFileDownload))
                {
                    UtilsSystem.RetryWhile(() => File.Delete(tmpFileDownload), (e) => true, 4000, this.Logger);
                }

                var url = $"/api/buildjobs/{job.jobId}/artifacts/{filename}";
                logger.LogInfo(true, "Downloading artifact from: '{0}' to '{1}'", url, tmpFileDownload);
                this.ExecuteApiCallToFile(url, tmpFileDownload);

                // Rename to the final cached artifact file
                logger.LogInfo(true, "Download succesful, moving to '{0}'", tmpFile);
                UtilsSystem.RetryWhile(() => File.Move(tmpFileDownload, tmpFile), (e) => true, 4000, this.Logger);
            }
            else
            {
                logger.LogInfo(true, "Skipping artifact download, already in local cache: {0}", tmpFile);
            }

            logger.LogInfo(true, "Unzipping {1} file to '{0}'...", destinationPath, UtilsSystem.BytesToString(new FileInfo(tmpFile).Length));

            ZipFile.ExtractToDirectory(tmpFile, destinationPath);

            logger.LogInfo(true, "Unzipping finished.");
        }

        /// <summary>
        /// Get a build object from it's id.
        /// </summary>
        /// <param name="version"></param>
        /// <param name="user"></param>
        /// <param name="project"></param>
        /// <returns></returns>
        public Build GetBuildFromVersion(string version, string user, string project)
        {
            var path =
                $"/api/projects/{HttpUtility.UrlEncode(user)}/{HttpUtility.UrlEncode(project)}/build/{HttpUtility.UrlEncode(version)}";

            var buildObj = this.ExecuteApiCall(path);

            var build = JsonConvert.DeserializeObject<Build>(buildObj["build"].ToString());
            build.project = JsonConvert.DeserializeObject<Project>(buildObj["project"].ToString());

            return build;
        }

        /// <summary>
        /// Find the last build job that succeeded. Maximum history introspection of 50 builds.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="project"></param>
        /// <param name="branch"></param>
        /// <param name="maxHistory"></param>
        /// <param name="buildVersionRequested"></param>
        /// <param name="exp"></param>
        /// <param name="maxResults">Maximum number of results to resturn</param>
        /// <returns></returns>
        public List<Build> FindLastSuccessfulBuilds(
            string user,
            string project,
            string branch = null,
            int maxHistory = 50,
            string buildVersionRequested = null,
            string exp = null,
            int maxResults = 1)
        {
            // TODO: This API endpoint has a lastbuild argument, that could be more reliable and optimum (to keep
            // track of last build artifact ID and only introspect any new builds since then).
            string path = $"/api/projects/{user}/{HttpUtility.UrlEncode(project)}/history";

            List<string> queryStringParts = new List<string>();

            queryStringParts.Add($"recordsNumber={maxHistory}");

            if (!string.IsNullOrWhiteSpace(branch))
            {
                queryStringParts.Add($"branch={HttpUtility.UrlEncode(branch)}");
            }

            // We add this here to ensure that no intermediate caches mess up with the data.
            queryStringParts.Add($"random={Guid.NewGuid()}");

            path += "?" + string.Join("&", queryStringParts);

            var items = this.ExecuteApiCall(path, null);

            List<Build> builds = new List<Build>();

            foreach (var jBuild in (JArray)items["builds"])
            {
                var build = JsonConvert.DeserializeObject<Build>(jBuild.ToString());

                if (!string.Equals(build.status, "success", StringComparison.CurrentCultureIgnoreCase))
                {
                    // this.Logger.LogInfo(true, $"Skipped build '{build.version}' with invalid status '{build.status}'");
                    continue;
                }

                // Only pass through specific version/buildid
                if (build.version != buildVersionRequested && !string.IsNullOrWhiteSpace(buildVersionRequested))
                {
                    // this.Logger.LogInfo(true, $"Skipped build '{build.version}' with unrequested version '{build.status}'");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(exp))
                {
                    Regex reg = new Regex(exp);
                    if (!reg.IsMatch(build.message))
                    {
                        // this.Logger.LogInfo(true, $"Skipped build '{build.version}' with unmatching build message regex '{exp}' '{build.message}'");
                        continue;
                    }
                }

                // The previous build object does not contain job information.
                build = this.GetBuildFromVersion(build.version, user, project);
                builds.Add(build);

                // This build has successful jobs... great.
                if (builds.Count >= maxResults)
                {
                    break;
                }
            }

            return builds;
        }

        /// <summary>
        /// Get the list of artifacts for a specific job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public List<Artifact> GetArtifactListFromJob(Job job)
        {
            string path = $"/api/buildjobs/{HttpUtility.UrlEncode(job.jobId)}/artifacts";

            JToken apiCallResult = this.ExecuteApiCall(path);

            return JsonConvert.DeserializeObject<List<Artifact>>(apiCallResult.ToString());
        }

        /// <summary>
        /// Prepares a WebClient with the needed auth 
        /// headers to attack the AppVeyor API
        /// </summary>
        /// <returns></returns>
        protected WebClient PrepareWebClient(string contentType = "application/json")
        {
            WebClient client = new WebClient();
            client.Headers.Set("Authorization", "Bearer " + this.Token);
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                client.Headers.Set("Content-Type", contentType);
            }

            return client;
        }

        /// <summary>
        /// Execute an API call
        /// </summary>
        /// <param name="uri">The remote URI</param>
        /// <param name="cacheFor">Cache the results for the specified amount of minutes. Defaults to 129600 minutes (three months). Use null for no cache.</param>
        /// <returns></returns>
        protected JToken ExecuteApiCall(
            string uri,
            int? cacheFor = 129600)
        {
            string cacheKey = "appveyor-client-" + uri;

            if (this.SimpleStore.Get<JToken>(cacheKey, out var item))
            {
                return item.Data;
            }

            var url = this.BaseUri + uri;
            WebClient client = this.PrepareWebClient();

            string result = null;

            try
            {
                result = client.DownloadString(url);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not download uri '{url}' {e.Message}", e);
            }

            var parsed = JToken.Parse(result);

            if (cacheFor > 0)
            {
                this.SimpleStore.Set(cacheKey, parsed, cacheFor.Value);
            }

            return parsed;
        }

        /// <summary>
        /// Run an API call and output results directly to a file
        /// mostly used to download artifacts...
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="localPath"></param>
        protected void ExecuteApiCallToFile(string uri, string localPath)
        {
            UtilsSystem.EnsureDirectoryExists(localPath);

            var url = this.BaseUri + uri;
            WebClient client = this.PrepareWebClient(null);
            client.DownloadFile(url, localPath);
        }
    }
}
