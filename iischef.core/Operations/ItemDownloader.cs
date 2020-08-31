using iischef.logger;
using iischef.utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace iischef.core.Operations
{
    /// <summary>
    /// 
    /// </summary>
    public class ItemDownloader
    {
        /// <summary>
        /// The logger.
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Uri to download this extension from
        /// </summary>
        protected ItemDownloaderConfig Config;

        /// <summary>
        /// The root path to the local artifact
        /// </summary>
        protected string LocalArtifactPath;

        /// <summary>
        /// Get an instance of ItemDownloader
        /// </summary>
        public ItemDownloader(
            ILoggerInterface logger,
            ItemDownloaderConfig config,
            string localArtifactPath)
        {
            this.Logger = logger;
            this.Config = config;
            this.LocalArtifactPath = localArtifactPath;
        }

        /// <summary>
        /// Execute the download
        /// </summary>
        /// <param name="destination"></param>
        public void Execute(string destination)
        {
            try
            {
                this.DoExecute(destination);
            }
            catch (Exception ex)
            {
                this.Logger.LogException(new Exception("Could not complete download of remote: " + destination, ex));
                this.DoExecute(destination, true);
            }
        }

        /// <summary>
        /// Execute the opreation...
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="forceDownload"></param>
        protected void DoExecute(
            string destination,
            bool forceDownload = false)
        {
            var uri = this.Config.uri;
            var maps = this.Config.maps;

            var filename = Path.GetFileName(uri);

            var tmpDir = UtilsSystem.GetTempPath("iischef_cache", UtilsEncryption.GetMD5(uri));
            var tmpFile = UtilsSystem.CombinePaths(UtilsSystem.GetTempPath(), UtilsEncryption.GetMD5(uri) + "_" + filename);

            if (forceDownload && Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }

            if (Directory.Exists(tmpDir))
            {
                var difo = new DirectoryInfo(tmpDir);
                if (!difo.EnumerateFiles("*", SearchOption.AllDirectories).Any())
                {
                    Directory.Delete(tmpDir, true);
                }
            }

            if (!Directory.Exists(tmpDir))
            {
                var parsedUri = new Uri(uri);
                if (parsedUri.Scheme.Equals("file", StringComparison.CurrentCultureIgnoreCase))
                {
                    var path = Path.Combine(this.LocalArtifactPath, parsedUri.LocalPath.TrimStart("\\".ToCharArray()));
                    File.Copy(path, tmpFile);
                }
                else
                {
                    using (var wc = new WebClient())
                    {
                        try
                        {
                            wc.Headers.Add(
                                "User-Agent",
                                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");
                            wc.DownloadFile(uri, tmpFile);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Could not download file: " + uri, ex);
                        }
                    }
                }

                UtilsSystem.EnsureDirectoryExists(tmpDir, true);

                if (tmpFile.EndsWith(".zip"))
                {
                    ZipFile.ExtractToDirectory(tmpFile, tmpDir);
                }
                else
                {
                    File.Move(tmpFile, UtilsSystem.CombinePaths(tmpDir, filename));
                }

                File.Delete(tmpFile);
            }

            // Move the files according to the maps
            foreach (var map in maps)
            {
                var files = (new DirectoryInfo(tmpDir)).GetFiles(map.Key, SearchOption.AllDirectories);

                if (!files.Any())
                {
                    throw new Exception(
                        string.Format(
                            "No matching files found for pattern: {0} in package {1} ['{2}']",
                            map.Key,
                            uri,
                            tmpDir));
                }

                if (files.Count() == 1)
                {
                    var dest = UtilsSystem.CombinePaths(destination, map.Value);
                    UtilsSystem.EnsureDirectoryExists(dest);
                    File.Copy(files.First().FullName, dest);
                }
                else
                {
                    foreach (var f in files)
                    {
                        var subpath = f.FullName.Replace((new DirectoryInfo(tmpDir)).FullName, string.Empty);
                        var dest = UtilsSystem.CombinePaths(destination, map.Value, subpath);
                        UtilsSystem.EnsureDirectoryExists(dest);

                        try
                        {
                            File.Copy(f.FullName, dest);
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Error copying file '{f.FullName}' to '{dest}'");
                        }
                    }
                }
            }
        }
    }
}
