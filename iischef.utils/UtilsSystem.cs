using iischef.logger;
using iischef.utils.WindowsAccount;
using NCode.ReparsePoints;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace iischef.utils
{
    public static class UtilsSystem
    {
        /// <summary>
        /// If we support long file system in current OS
        /// </summary>
        public static readonly bool FileSystemSupportsUnicodeFileNames = false;

        /// <summary>
        /// Reserved file names in windows
        /// </summary>
        public static List<string> InvalidWindowsFileNames = new List<string>()
        {
            "con",
            "nul",
            "aux",
            "clock$",
            "prn",
            "com1",
            "com2",
            "com3",
            "com4",
            "com5",
            "com6",
            "com7",
            "com8",
            "com9",
            "lpt1",
            "lpt2",
            "lpt3",
            "lpt4",
            "lpt5",
            "lpt6",
            "lpt7",
            "lpt8",
            "lpt9"
        };

        /// <summary>
        /// 
        /// </summary>
        private const string UnicodePathPrefix = "\\\\?\\";

        /// <summary>
        /// Static constructor
        /// </summary>
        static UtilsSystem()
        {
            try
            {
                DoTestUnicodePaths();
                FileSystemSupportsUnicodeFileNames = true;
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Check if a windows feature is enabled
        /// </summary>
        /// <param name="featureName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static bool IsWindowsFeatureEnabled(string featureName, ILoggerInterface logger)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddCommand("Get-WindowsOptionalFeature");
                ps.AddParameter("Online", true);
                ps.AddParameter("FeatureName", featureName);
                var res = ps.InvokeAndTreatError(logger);
                return res.Count > 0 && res.First().Members["State"]?.Value?.ToInt32() == 2;
            }
        }

        /// <summary>
        /// Tests unicode paths, exception thrown if anything goes wrong
        /// or unicode paths are not supported
        /// </summary>
        public static void DoTestUnicodePaths()
        {
            var longFileNameDir = Path.Combine(Path.GetTempPath(), "unicodetest");

            if (Directory.Exists(longFileNameDir))
            {
                Directory.Delete(longFileNameDir);
            }

            Directory.CreateDirectory(UnicodePathPrefix + longFileNameDir);
        }

        /// <summary>
        /// Find the size in disk of string
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static long GetStringSizeInDiskBytes(string data, Encoding encoding = null)
        {
            string fileTempPath = Path.GetTempFileName();
            File.WriteAllText(fileTempPath, data, encoding ?? Encoding.UTF8);
            long size = new FileInfo(fileTempPath).Length;
            File.Delete(fileTempPath);
            return size;
        }

        /// <summary>
        /// Check if a directory is wrtibale. Returns exception if not.
        /// </summary>
        /// <returns></returns>
        public static bool TestCanWriteFile(string filePath, ILoggerInterface logger)
        {
            try
            {
                // Check if the file exists
                if (File.Exists(filePath))
                {
                    // If the file exists, try to open it with write access and then close it
                    using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Write))
                    {
                        // Do nothing, just check if it can be opened with write access
                    }
                }
                else
                {
                    // If the file does not exist, try to create it and then delete it
                    using (FileStream fileStream = File.Create(filePath))
                    {
                        // Do nothing, just check if it can be created
                    }

                    File.Delete(filePath);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0)
            {
                return "0" + suf[0];
            }

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string EscapeStringForDirectoryName(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidPathChars();
            StringBuilder escapedString = new StringBuilder();
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringInfo = new StringInfo(normalizedString);

            for (int index = 0; index < stringInfo.LengthInTextElements; index++)
            {
                string textElement = stringInfo.SubstringByTextElements(index, 1);
                var category = CharUnicodeInfo.GetUnicodeCategory(textElement, 0);

                if (category != UnicodeCategory.NonSpacingMark)
                {
                    char c = textElement[0];

                    if (Array.IndexOf(invalidChars, c) < 0)
                    {
                        escapedString.Append(c);
                    }
                    else
                    {
                        escapedString.Append('_'); // Replace invalid characters with a placeholder character (e.g., '_')
                    }
                }
            }

            return escapedString.ToString();
        }

        /// <summary>
        /// Load a string resource copied as content for current executiona assembly
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetResourceFileAsString(string path)
        {
            return File.ReadAllText(AssemblyDirectory + "\\" + path);
        }

        /// <summary>
        /// Get the runtime path to a resource file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetResourceFileAsPath(string path)
        {
            return AssemblyDirectory + "\\" + path;
        }

        /// <summary>
        /// Ensure a directory exist.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static string DirectoryCreateIfNotExists(string dir)
        {
            dir = EnsureLongPathSupportIfAvailable(dir);

            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception e)
                {
                    throw new Exception($"Cannot create directory '{dir}'", e);
                }
            }

            return dir;
        }

        /// <summary>
        /// Save an embeded resource as a file
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="resourceName"></param>
        /// <param name="fileName"></param>
        public static void EmbededResourceToFile(Assembly assembly, string resourceName, string fileName)
        {
            if (File.Exists(fileName))
            {
                throw new Exception("Destination file sould not exist.");
            }

            resourceName = assembly.GetName().Name + "." + resourceName;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var fileStream = File.Create(fileName))
                {
                    stream?.CopyTo(fileStream);
                }
            }
        }

        public static string GetEmbededResourceAsString(Assembly assembly, string resourceName)
        {
            resourceName = assembly.GetName().Name + "." + resourceName;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Ensure a directory exists. Returns path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isDir"></param>
        public static string EnsureDirectoryExists(string path, bool isDir = false)
        {
            var dir = isDir ? path : Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return path;
        }

        /// <summary>
        /// Check if we are running in continuous integration
        /// </summary>
        /// <returns></returns>
        public static bool RunningInContinuousIntegration()
        {
            return "True".Equals(Environment.GetEnvironmentVariable("CI"), StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <param name="condition"></param>
        /// <param name="selector"></param>
        /// <param name="name"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static List<T2> QueryEnumerable<T, T2>(
            IEnumerable<T> source,
            Func<T, bool> condition,
            Func<T, T2> selector,
            Func<T, string> name,
            ILoggerInterface logger)
        {
            var results = new List<T2>();

            foreach (var s in source)
            {
                bool isMatch = false;

                try
                {
                    isMatch = condition(s);
                }
                catch (Exception e)
                {
                    string displayName = null;

                    try
                    {
                        displayName = name(s);
                    }
                    catch
                    {
                        // ignored
                    }

                    logger.LogWarning(
                        true,
                        "Error while inspecting condition on object {0}: {1}",
                        displayName,
                        e.Message);
                }

                if (!isMatch)
                {
                    continue;
                }

                results.Add(selector(s));
            }

            return results;
        }

        /// <summary>
        /// Wait for an operation to complete
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="maxWaitMilliseconds"></param>
        /// <param name="waitMessage"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static bool WaitWhile(
            Func<bool> condition,
            int maxWaitMilliseconds,
            string waitMessage,
            ILoggerInterface logger)
        {
            int sleep = 500;
            int sleepStep = 1000;

            Stopwatch sw;

            sw = Stopwatch.StartNew();
            sw.Start();

            while (condition() && sw.ElapsedMilliseconds < maxWaitMilliseconds)
            {
                logger.LogInfo(true, waitMessage);
                Thread.Sleep(sleep);
                sleep += sleepStep;
            }

            return !condition();
        }

        /// <summary>
        /// Retry an action while the exception meets the condition during the maximum wait specified
        /// </summary>
        /// <param name="task"></param>
        /// <param name="condition"></param>
        /// <param name="maxWaitMilliseconds">Max milliseconds for the operation to complete</param>
        /// <param name="logger"></param>
        /// <param name="minRetries"></param>
        public static T RetryWhile<T>(
            Func<T> task,
            Func<Exception, bool> condition,
            int maxWaitMilliseconds,
            ILoggerInterface logger,
            int minRetries = 2)
        {
            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            int sleep = 250;
            int sleepStep = 400;
            int failCount = 0;

            while (true)
            {
                try
                {
                    T result = task();

                    if (failCount > 0)
                    {
                        logger?.LogInfo(true, "Operation completed.");
                    }

                    return result;
                }
                catch (Exception e)
                {
                    // If the looping condition is not met, throw the exception.
                    if (!condition(e))
                    {
                        ExceptionDispatchInfo.Capture(e).Throw();
                    }

                    // If we have reached the maximum wait limit plus we have failed at least once, abort.
                    if (sw.ElapsedMilliseconds > maxWaitMilliseconds && failCount >= minRetries)
                    {
                        throw new Exception(
                            $"Transient error did not go away after waiting for {maxWaitMilliseconds}ms and failing {failCount} times...",
                            e);
                    }

                    failCount++;

                    string errorMessage = e.Message;

                    if (e is AggregateException aggregateException)
                    {
                        errorMessage += "(" + string.Join(
                            ", ",
                            aggregateException.InnerExceptions.Select((i) => i.Message)) + ")";
                    }

                    logger?.LogInfo(true, "Found transient error: {0}", errorMessage);
                    logger?.LogInfo(true, "Retrying operation...");
                    Thread.Sleep(sleep);
                    sleep = sleep + sleepStep;
                }
            }
        }

        /// <summary>
        /// Copy files beteween two directories
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreErrors"></param>
        public static void CopyFilesRecursively(
            DirectoryInfo source,
            DirectoryInfo target,
            bool overwrite = false,
            bool ignoreErrors = false)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), overwrite);
            }

            foreach (FileInfo file in source.GetFiles())
            {
                try
                {
                    file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite);
                }
                catch
                {
                    if (!ignoreErrors)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Recursive copy using threads. Support long path names.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreOnDeployPattern"></param>
        /// <param name="logger"></param>
        /// <param name="changedSince"></param>
        public static void CopyFilesRecursivelyFast(
            string source,
            string target,
            bool overwrite,
            string ignoreOnDeployPattern,
            ILoggerInterface logger,
            DateTime? changedSince = null)
        {
            source = EnsureLongPathSupportIfAvailable(source);
            target = EnsureLongPathSupportIfAvailable(target);

            DoCopyFilesRecursivelyFast(
                new DirectoryInfo(source),
                new DirectoryInfo(target),
                overwrite,
                ignoreOnDeployPattern,
                logger,
                changedSince);
        }

        /// <summary>
        /// Copy files recursively.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreOnDeployPattern"></param>
        /// <param name="logger"></param>
        /// <param name="changedSince">Solo copiar ficheros modificados desde la fecha</param>
        private static void DoCopyFilesRecursivelyFast(
            DirectoryInfo source,
            DirectoryInfo target,
            bool overwrite,
            string ignoreOnDeployPattern,
            ILoggerInterface logger,
            DateTime? changedSince)
        {
            var files = source.EnumerateFiles("*", SearchOption.AllDirectories);
            ParallelOptions pop = new ParallelOptions();

            // The bottle neck here is disk rather than CPU... but number of CPU's is a good measure
            // of how powerful the target machine might be...
            pop.MaxDegreeOfParallelism = (int)Math.Ceiling(Environment.ProcessorCount * 1.5);

            logger.LogInfo(
                true,
                "Copying files from {1} to {2} with {0} threads.",
                pop.MaxDegreeOfParallelism,
                source.FullName,
                target.FullName);

            var ignoreOnDeployRegex = string.IsNullOrWhiteSpace(ignoreOnDeployPattern)
                ? null
                : new Regex(ignoreOnDeployPattern);

            int filesCopied = 0;
            int filesSkipped = 0;

            using (ProgressLogger pl = new ProgressLogger(logger))
            {
                pl.StartProgress();

                void WriteProgress()
                {
                    pl.DoWrite($"Copied {filesCopied} files (skipped {filesSkipped})");
                }

                Parallel.ForEach(files, pop, (i) =>
                {
                    string dir;

                    try
                    {
                        dir = i.Directory.FullName;

                        var relativeDir =
                            dir.Substring(source.FullName.Length, dir.Length - source.FullName.Length)
                                .TrimStart("\\".ToCharArray());

                        var relativeFile = Path.Combine(relativeDir, i.Name);

                        var destDir = Path.Combine(target.FullName, relativeDir);
                        var destFile = new FileInfo(Path.Combine(destDir, i.Name));

                        if (ignoreOnDeployRegex?.IsMatch(relativeFile) == true)
                        {
                            Interlocked.Add(ref filesSkipped, 1);
                            WriteProgress();
                            return;
                        }

                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        if (changedSince != null && i.LastWriteTimeUtc < changedSince)
                        {
                            Interlocked.Add(ref filesSkipped, 1);
                            WriteProgress();
                            return;
                        }

                        if ((!destFile.Exists) || (destFile.Exists && overwrite))
                        {
                            i.CopyTo(destFile.FullName, true);
                            Interlocked.Add(ref filesCopied, 1);
                        }
                    }
                    catch (Exception e)
                    {
                        bool throwException = true;

                        // Reserved names cannot be copied...
                        if (throwException == true && InvalidWindowsFileNames.Contains(i.Name.ToLower()))
                        {
                            logger.LogWarning(true, "Skipped invalid file: " + i.FullName);
                            pl.ResetWrittenLength();
                            throwException = false;
                        }

                        if (throwException == true && e is DirectoryNotFoundException && UtilsJunction.IsJunctionOrSymlink(i.FullName))
                        {
                            var link = ReparsePointFactory.Create().GetLink(i.FullName);

                            if (!Directory.Exists(link.Target))
                            {
                                logger.LogWarning(
                                    true,
                                    "Skipped link '{0}' with broken destination '{1}'",
                                    i.FullName,
                                    link.Target);

                                throwException = false;
                            }
                        }

                        if (throwException)
                        {
                            throw new Exception("Error copying file: " + i.FullName, e);
                        }
                    }

                    WriteProgress();
                });

                pl.ProgressEndAndPersist();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string GetCodeBaseDir()
        {
            string codeBasePath = Assembly.GetExecutingAssembly().CodeBase;
            codeBasePath = codeBasePath.Replace("file:///", string.Empty);
            codeBasePath = Path.GetDirectoryName(codeBasePath);
            return codeBasePath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string FindResourcePhysicalPath(Type type, string path)
        {
            string codeBasePath = UtilsSystem.GetCodeBaseDir();

            var finalparts = NormalizeResourcePath(type, path);

            // There is a problem with the root path of the assembly,
            // so start removing leading namespaces until we find a physical match.
            for (int x = 0; x < 5; x++)
            {
                List<string> pathParts = new List<string>();
                pathParts.Add(codeBasePath);
                pathParts.AddRange(finalparts.Skip(x).Take(finalparts.Count - x));

                string pathAsFile = CombinePaths(pathParts.ToArray());

                if (File.Exists(pathAsFile) || Directory.Exists(pathAsFile))
                {
                    return pathAsFile;
                }
            }

            return null;
        }

        public static List<string> NormalizeResourcePath(Type type, string path)
        {
            var pathparts = type.FullName.Split(".".ToCharArray()).ToList();
            pathparts.RemoveAt(pathparts.Count - 1);

            var requestedparts = path.Split("/".ToCharArray()).ToList();

            List<string> finalparts = new List<string>();
            finalparts.AddRange(pathparts);
            foreach (var p in requestedparts)
            {
                if (p == "..")
                {
                    finalparts.RemoveAt(finalparts.Count - 1);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(p))
                {
                    continue;
                }

                finalparts.Add(p);
            }

            return finalparts;
        }

        /// <summary>
        /// Returns a temprorary path that DOES exist. Some
        /// environment settings might generate per-session temp
        /// paths that are not initialized.
        /// </summary>
        /// <returns></returns>
        public static string GetTempPath(params string[] parts)
        {
            var pathParts = parts.ToList();
            pathParts.Insert(0, Path.GetTempPath());

            var path = CombinePaths(pathParts.ToArray<string>());

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>
        /// Combine several path parts.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static string CombinePaths(params string[] paths)
        {
            for (int x = 0; x < paths.Count(); x++)
            {
                if (paths[x] != null)
                {
                    paths[x] = paths[x].Replace("/", "\\");
                }
            }

            string result = paths.First();

            for (int x = 1; x < paths.Count(); x++)
            {
                string variable = paths[x];

                if (variable == null)
                {
                    continue;
                }

                // Eliminamos los trailing "\\" para evitar  que solo devuelva el segundo path
                while (variable.Length > 0 && variable.Substring(0, 1) == "\\")
                {
                    variable = variable.Substring(1, variable.Length - 1);
                }

                if (variable != string.Empty)
                {
                    result = Path.Combine(result, variable);
                }
            }

            return result;
        }

        /// <summary>
        /// Make sure that a directory that is being deleted, is within a minimum
        /// directory depth to prevent uninteded deletion of root folders
        /// </summary>
        /// <param name="path"></param>
        public static void ValidateDirectoryDepthDeletion(string path)
        {
            int directoryDepth = 3;

            if (!Directory.Exists(path))
            {
                return;
            }

            var difo = new DirectoryInfo(path);

            if (difo.FullName.Split("\\".ToCharArray()).Length < directoryDepth)
            {
                throw new InvalidOperationException(
                    $"Cannot delete directories with a depth lower than {directoryDepth}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool ExceptionIsAccessDeniedOrFileInUse(Exception e)
        {
            List<uint> errorCodes = new List<uint>();

            // Code for the error "The process cannot access the file XXX"
            errorCodes.Add(0x80070020);

            // Code for "Access to the path is denied"
            errorCodes.Add(0x80070005);
            errorCodes.Add(0x7FF8FFFB);

            List<Exception> exceptions = new List<Exception>();
            exceptions.Add(e);

            if (e is AggregateException aggregateException)
            {
                exceptions.AddRange(aggregateException.InnerExceptions);
            }

            foreach (var ex in exceptions)
            {
                uint errorHResult = (uint)ex.HResult;
                if (errorCodes.Contains(errorHResult))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Delete a directory
        /// </summary>
        /// <param name="strDir"></param>
        /// <param name="logger"></param>
        /// <param name="waitTimeIfInUse"></param>
        public static void DeleteDirectory(
            string strDir,
            ILoggerInterface logger,
            int waitTimeIfInUse = 10)
        {
            DoDeleteDirectory(strDir, logger, null, waitTimeIfInUse);
        }

        /// <summary>
        /// Delete a directory, and detect and close any processes that might be holding a handle
        /// </summary>
        /// <param name="strDir"></param>
        /// <param name="logger"></param>
        /// <param name="closeProcesses"></param>
        /// <param name="waitTimeIfInUse"></param>
        public static void DeleteDirectoryAndCloseProcesses(
            string strDir,
            ILoggerInterface logger,
            List<string> closeProcesses,
            int waitTimeIfInUse = 10)
        {
            DoDeleteDirectory(
                strDir,
                logger,
                closeProcesses,
                waitTimeIfInUse);
        }

        /// <summary>
        /// Delete a directory
        /// </summary>
        /// <param name="strDir">Directory to delete</param>
        /// <param name="logger"></param>
        /// <param name="closeProcesses">Force a process close if it cannot be deleted (i.e. in use)</param>
        /// <param name="waitTimeIfInUse">If in-use, time to wait (in seconds) before either failing or closing all processes if forceCloseProcesses is true.</param>
        private static void DoDeleteDirectory(
            string strDir,
            ILoggerInterface logger,
            List<string> closeProcesses = null,
            int waitTimeIfInUse = 10)
        {
            if (string.IsNullOrWhiteSpace(strDir))
            {
                logger.LogWarning(true, "Empty directory name provided DoDeleteDirectory, skipping.");
                return;
            }

            ValidateDirectoryDepthDeletion(strDir);
            strDir = EnsureLongPathSupportIfAvailable(strDir);

            if (!Directory.Exists(strDir))
            {
                return;
            }

            logger.LogInfo(
                true,
                "Removing directory {0} with close processes {1}",
                strDir,
                closeProcesses == null ? string.Empty : string.Join(", ", closeProcesses));

            if (closeProcesses?.Any() == true)
            {
                var processes = UtilsProcess.GetPathProcessesInfo(strDir, logger, true);

                foreach (var p in processes.AsIterable())
                {
                    logger.LogWarning(
                        false,
                        "The following process might be blocking files in the directory: {0}",
                        p.CommandLine);
                }

                if (processes.Any())
                {
                    UtilsProcess.ClosePathProcesses(strDir, closeProcesses, logger);
                }
            }

            RetryWhile(
                () =>
                {
                    DeleteDirectoryAndRemovePermissionsIfNeeded(strDir, logger);
                    return true;
                },
                ExceptionIsAccessDeniedOrFileInUse,
                waitTimeIfInUse * 1000,
                logger);

            if (Directory.Exists(strDir))
            {
                throw new Exception($"Could not completely delete directory '{strDir}', see log for details.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static long GetDirectorySize(string strDir)
        {
            var directories = Directory.EnumerateFiles(strDir, "*", SearchOption.AllDirectories);

            long size = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? Environment.ProcessorCount - 1 : 1
            };

            Parallel.ForEach(
                directories,
                parallelOptions,
                (f, loopState) =>
                {
                    Interlocked.Add(ref size, new FileInfo(f).Length);
                });

            return size;
        }

        public static void DeleteFile(
                string file,
                ILoggerInterface logger,
                int waitTimeIfInUse = 10)
        {
            ValidateDirectoryDepthDeletion(file);
            file = EnsureLongPathSupportIfAvailable(file);

            if (!File.Exists(file))
            {
                return;
            }

            RetryWhile(
                () =>
                {
                    File.Delete(file);
                    return true;
                },
                ExceptionIsAccessDeniedOrFileInUse,
                waitTimeIfInUse * 1000,
                logger);

            if (File.Exists(file))
            {
                throw new Exception($"Could not delete file '{file}' see log for details.");
            }
        }

        /// <summary>
        /// Make sure that the directory exists.
        /// </summary>
        /// <param name="path"></param>
        public static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Cambia los atributos de un fichero quitándole el atributo "ReadOnly"
        /// </summary>
        /// <param name="strFile"></param>
        public static void SetNotReadOnlyFile(string strFile)
        {
            FileInfo oFile = new FileInfo(strFile);
            if (oFile.Exists)
            {
                if ((oFile.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    oFile.Attributes -= FileAttributes.ReadOnly;
                }

                File.SetAttributes(strFile, File.GetAttributes(strFile) & ~FileAttributes.System);
                File.SetAttributes(strFile, File.GetAttributes(strFile) & ~FileAttributes.Hidden);
                File.SetAttributes(strFile, File.GetAttributes(strFile) & ~(FileAttributes.Archive | FileAttributes.ReadOnly));
            }

            oFile = null;
        }

        public static void SetNotReadOnlyDirectory(string strDir)
        {
            DirectoryInfo oDir = new DirectoryInfo(strDir);

            if (oDir.Exists)
            {
                if ((oDir.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    oDir.Attributes -= FileAttributes.ReadOnly;
                }

                File.SetAttributes(strDir, File.GetAttributes(strDir) & ~FileAttributes.System);
                File.SetAttributes(strDir, File.GetAttributes(strDir) & ~FileAttributes.Hidden);
                File.SetAttributes(strDir, File.GetAttributes(strDir) & ~(FileAttributes.Archive | FileAttributes.ReadOnly));
            }

            oDir = null;
        }

        /// <summary>
        /// Delete a directory the fastest way possible, throws exception if anything fails.
        ///
        /// Symlinks are ignored and cleared prior to deletion.
        /// 
        /// </summary>
        /// <param name="strDir"></param>
        private static void DeleteDirectoryAndRemovePermissionsIfNeeded(string strDir, ILoggerInterface logger)
        {
            ValidateDirectoryDepthDeletion(strDir);
            strDir = EnsureLongPathSupportIfAvailable(strDir);

            if (!Directory.Exists(strDir))
            {
                return;
            }

            // If the directory is a symlink, remove it DIRECTLY without internal cleanup
            if (UtilsJunction.IsJunctionOrSymlink(strDir))
            {
                UtilsJunction.DeleteJunctionOrSymlink(strDir);
                logger.LogInfo(true, "Directory was a junction, and the junction has been deleted (junction target preserved)");
                return;
            }

            long freedSpace = 0;
            long processedFiles = 0;
            long processedDirectories = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? Environment.ProcessorCount - 1 : 1
            };

            // To prevent symlink introspection, these are removed first.
            using (ProgressLogger progressLogger = new ProgressLogger(logger))
            {
                progressLogger.StartProgress();

                DeleteJunctionsOrLinks(strDir, (message) => progressLogger.DoWrite(message), logger);
                progressLogger.ProgressEndAndPersist();

                int exceptionBreakCount = 100;
                var exceptions = new ConcurrentQueue<Exception>();

                void WriteProgress()
                {
                    progressLogger.DoWrite(
                        $"Total deleted {processedFiles} files and {processedDirectories} directories ({UtilsSystem.BytesToString(freedSpace)})");
                }

                WriteProgress();

                Parallel.ForEach(
                    Directory.EnumerateFiles(strDir, "*", SearchOption.AllDirectories),
                    parallelOptions,
                    (f, loopState) =>
                    {
                        try
                        {
                            var fileSize = new FileInfo(f).Length;
                            DeleteSingleFile(f);
                            Interlocked.Add(ref processedFiles, 1);
                            Interlocked.Add(ref freedSpace, fileSize);
                            WriteProgress();
                        }
                        catch (Exception e)
                        {
                            exceptions.Enqueue(e);
                        }

                        if (exceptions.Count > exceptionBreakCount)
                        {
                            loopState.Break();
                        }
                    });

                DeleteDirectoryRecursive(
                    strDir,
                    new ConcurrentDictionary<Guid, Task>(),
                    (dir) =>
                    {
                        Interlocked.Add(ref processedDirectories, 1);
                        WriteProgress();
                    },
                    parallelOptions.MaxDegreeOfParallelism);

                progressLogger.ProgressEndAndPersist();

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory">Directory to delete</param>
        /// <param name="tasks">Currently active parallel tasks. Pass empty dictionary at initial call.</param>
        /// <param name="deletedDirectory">Action to be called for every deleted directory.</param>
        /// <param name="maxConcurrentThreads">Maximum number of concurrent threads</param>
        private static void DeleteDirectoryRecursive(string directory, ConcurrentDictionary<Guid, Task> tasks, Action<string> deletedDirectory, int maxConcurrentThreads)
        {
            ConcurrentDictionary<Guid, Task> childTasks = new ConcurrentDictionary<Guid, Task>();

            foreach (var enumerateDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (tasks?.Count < maxConcurrentThreads)
                {
                    var taskId = Guid.NewGuid();

                    var task = Task.Factory.StartNew(() =>
                    {
                        DeleteDirectoryRecursive(enumerateDirectory, tasks, deletedDirectory, maxConcurrentThreads);
                        tasks.TryRemove(taskId, out _);
                    });

                    if (tasks.TryAdd(taskId, task))
                    {
                        childTasks.TryAdd(taskId, task);
                    }
                    else
                    {
                        // If we could not add it, then just wait for it.
                        task.Wait();
                    }
                }
                else
                {
                    DeleteDirectoryRecursive(enumerateDirectory, tasks, deletedDirectory, maxConcurrentThreads);
                }
            }

            // Wait for al children to be completed
            Task.WaitAll(childTasks.Values.ToArray());
            deletedDirectory(directory);
            DeleteSingleDirectory(directory);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPattern"></param>
        /// <param name="searchOption"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static IEnumerable<string> EnumerateDirectoriesInternal(
            string path,
            string searchPattern,
            SearchOption searchOption,
            ILoggerInterface logger)
        {
            try
            {
                return Directory.EnumerateDirectories(path, searchPattern, searchOption);
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                logger.LogWarning(false, "Access denied to path {0}. Trying to set ADMINISTRATOR rights.", path);

                UtilsWindowsAccounts.SetUniqueAclForIdentity(
                    UtilsWindowsAccounts.WELL_KNOWN_SID_ADMINISTRATORS,
                    fileSystemRights: FileSystemRights.FullControl,
                    type: AccessControlType.Allow,
                    directory: path,
                    logger: logger);

                return Directory.EnumerateDirectories(path, searchPattern, searchOption);
            }
        }

        /// <summary>
        /// Clear top-down junctions or links in a directory structure.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="writeProgress"></param>
        /// <param name="logger"></param>
        private static void DeleteJunctionsOrLinks(string directory, Action<string> writeProgress, ILoggerInterface logger)
        {
            int deleted = 0;
            int count = 1;
            ConcurrentBag<string> stack = new ConcurrentBag<string>();
            stack.Add(directory);

            void ProgressWriter() => writeProgress($"Symlink directory cleanup deleted {deleted} symlinks from {count} directories.");

            ProgressWriter();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? Environment.ProcessorCount - 1 : 1
            };

            while (stack.Count != 0)
            {
                var stackClone = stack;
                stack = new ConcurrentBag<string>();

                Parallel.ForEach(
                    stackClone,
                    parallelOptions,
                    (f, loopState) =>
                    {
                        foreach (var dir in EnumerateDirectoriesInternal(f, "*", SearchOption.TopDirectoryOnly, logger))
                        {
                            Interlocked.Add(ref count, 1);
                            ProgressWriter();

                            if (UtilsJunction.IsJunctionOrSymlink(dir))
                            {
                                Interlocked.Add(ref deleted, 1);
                                UtilsJunction.DeleteJunctionOrSymlink(dir);
                            }
                            else
                            {
                                stack.Add(dir);
                            }
                        }
                    });
            }

            ProgressWriter();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strDir"></param>
        private static void DeleteSingleDirectory(string strDir)
        {
            if (string.IsNullOrWhiteSpace(strDir))
            {
                return;
            }

            if (!Directory.Exists(strDir))
            {
                return;
            }

            try
            {
                Directory.Delete(strDir, true);
                return;
            }
            catch
            {
                // ignored
            }

            if (!Directory.Exists(strDir))
            {
                return;
            }

            try
            {
                SetNotReadOnlyDirectory(strDir);
            }
            catch
            {
                // ignored
            }

            try
            {
                Directory.Delete(strDir, true);
            }
            catch
            {
                // ignored
            }
        }

        private static void DeleteSingleFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }

            try
            {
                File.Delete(file);
                return;
            }
            catch
            {
                // In file system's without long file name support, this is almost impossible from within the windows API's'
                if (InvalidWindowsFileNames.Contains(Path.GetFileName(file).ToLower()))
                {
                    throw new Exception("Cannot remove file because it uses a protected file name. Remove manually.  " + file);
                }

                // ignored
            }

            try
            {
                SetNotReadOnlyFile(file);
            }
            catch
            {
                // ignored
            }

            File.Delete(file);
        }

        /// <summary>
        /// Check if this is a network or a local path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsNetworkPath(string path)
        {
            if (path.StartsWith(@"\\") && path.Length >= 3)
            {
                // Check if the path starts with "\\" and has at least two more characters after that.
                return true;
            }

            return false;
        }

        /// <summary>
        /// Si es un path de red, resolviendo el link si lo hubiera
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsNetworkPathResolveLinks(string path)
        {
            path = RealPath(path);

            if (UtilsSystem.IsNetworkPath(path))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string RealPath(string path)
        {
            return UtilsJunction.ResolvePath(path);
        }

        /// <summary>
        /// Total free space for the given drive of a path in BYTES
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static long GetTotalFreeSpace(string path)
        {
            string driveName = Path.GetPathRoot(path);

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && string.Equals(drive.Name, driveName, StringComparison.OrdinalIgnoreCase))
                {
                    return drive.TotalFreeSpace;
                }
            }

            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool IsSmallDirectory(string directoryPath, int threshold)
        {
            try
            {
                return GetItemCountRecursively(new DirectoryInfo(directoryPath), threshold) <= threshold;
            }
            catch (UnauthorizedAccessException)
            {
                // Nothing, there are situations where a directory might no event have an ACL
                // and though as admin you can set it, you cannot enumerate it's contents.
                return false;
            }
        }

        private static int GetItemCountRecursively(DirectoryInfo directoryInfo, int threshold, int currentCount = 0)
        {
            foreach (var item in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                currentCount++;

                if (item is DirectoryInfo dir)
                {
                    currentCount = GetItemCountRecursively(dir, threshold, currentCount);
                }

                if (currentCount > threshold)
                {
                    break;
                }
            }

            return currentCount;
        }

        /// <summary>
        /// Download the contents of a remote URL as plain text.
        /// * Does not fail on SSL validation errors (for self signed certificates, etc..)
        /// * Retries the download to deal with connection glitches
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="retry"></param>
        /// <param name="expectedCode"></param>
        /// <param name="headers"></param>
        /// <param name="forceIpAddress">The request will be forced to a specific IP address</param>
        /// <returns></returns>
        public static string DownloadUriAsText(
            string uri,
            HttpStatusCode expectedCode = HttpStatusCode.OK,
            Dictionary<string, string> headers = null,
            string forceIpAddress = null)
        {
            string overrideHost = null;

            if (!string.IsNullOrWhiteSpace(forceIpAddress))
            {
                var requestUri = new Uri(uri);
                overrideHost = requestUri.Host;
                var builder = new UriBuilder(uri);
                builder.Host = forceIpAddress;
                uri = builder.ToString();
            }

            using (HttpClient client = new HttpClient())
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(overrideHost))
                {
                    client.DefaultRequestHeaders.Host = overrideHost;
                }

                // Change SSL checks so that all checks pass
                // Note: This is a risky operation and should be used cautiously
                ServicePointManager.ServerCertificateValidationCallback =
                    new RemoteCertificateValidationCallback(
                        delegate { return true; });

                HttpResponseMessage httpResponse;

                string response = null;

                try
                {
                    httpResponse = client.GetAsync(uri).Result;

                    response = httpResponse.Content.ReadAsStringAsync().Result;

                    if (httpResponse.StatusCode != expectedCode)
                    {
                        throw new CustomWebException(
                            $"Unexpected status code at {uri}. Received {httpResponse.StatusCode} expected {expectedCode}",
                            (int)httpResponse.StatusCode,
                            response);
                    }
                }
                catch (HttpRequestException httpRequestException)
                {
                    ExceptionDispatchInfo.Capture(httpRequestException).Throw();
                }
                catch (Exception ex)
                {
                    if (ex is AggregateException aggregateException &&
                        aggregateException.InnerExceptions.Count == 1)
                    {
                        ExceptionDispatchInfo.Capture(aggregateException.InnerExceptions.First()).Throw();
                    }

                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
                finally
                {
                    // Restore SSL validation behaviour
                    ServicePointManager.ServerCertificateValidationCallback = null;
                }

                return response;
            }
        }

        /// <summary>
        /// Add long file name support if this is supported by the current OS
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string EnsureLongPathSupportIfAvailable(string path)
        {
            if (!FileSystemSupportsUnicodeFileNames)
            {
                return path;
            }

            return AddLongPathSupport(path);
        }

        /// <summary>
        /// Add long file name support to a path. Only here for testing, use EnsureLongPathSupportIfAvailable that
        /// will automatically detect support for long paths.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string AddLongPathSupport(string path)
        {
            // Network paths
            if (path.StartsWith(UnicodePathPrefix))
            {
                return path;
            }

            if (IsNetworkPath(path))
            {
                if (path.StartsWith("\\\\"))
                {
                    path = path.Substring(2, path.Length - 2);
                }

                return UnicodePathPrefix + "UNC\\" + path;
            }

            return UnicodePathPrefix + path;
        }

        /// <summary>
        /// Remove long path support
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string RemoveLongPathSupport(string path)
        {
            string prefix1 = UnicodePathPrefix;
            string prefix2 = UnicodePathPrefix + "UNC\\";

            if (path.StartsWith(prefix2))
            {
                return "\\\\" + path.ReplaceFirst(prefix2, string.Empty);
            }

            if (path.StartsWith(prefix1))
            {
                return path.ReplaceFirst(prefix1, string.Empty);
            }

            return path;
        }
    }
}
