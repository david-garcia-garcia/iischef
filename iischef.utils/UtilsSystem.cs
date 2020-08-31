using iischef.logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Principal;
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
        /// Most usual processes
        /// </summary>
        public static List<string> DefaultProcessWhitelist = new List<string>()
        {
            "explorer.exe",
            "w3wp.exe",
            "php-cgi.exe",
            "php.exe"
        };

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
                var longFileNameDir = AssemblyDirectory + "\\unicodetest";
                if (Directory.Exists(longFileNameDir))
                {
                    Directory.Delete(longFileNameDir);
                }

                Directory.CreateDirectory(UnicodePathPrefix + longFileNameDir);

                FileSystemSupportsUnicodeFileNames = true;
            }
            catch
            {
                // ignored
            }
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
        public static List<T2> QueryEnumerable<T, T2>(IEnumerable<T> source, Func<T, bool> condition, Func<T, T2> selector, Func<T, string> name, ILoggerInterface logger)
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

                    logger.LogWarning(true, "Error while inspecting condition on object {0}: {1}", displayName, e.Message);
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
        /// <param name="maxWait">Max milliseconds for the operation to complete</param>
        /// <param name="logger"></param>
        /// <param name="minRetries"></param>
        public static void RetryWhile(
            Action task,
            Func<Exception, bool> condition,
            int maxWait,
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
                    task();

                    if (failCount > 0)
                    {
                        logger?.LogInfo(true, "Operation completed.");
                    }

                    return;
                }
                catch (Exception e)
                {
                    // If the looping condition is not met, throw the exception.
                    if (!condition(e))
                    {
                        throw;
                    }

                    // If we have reached the maximum wait limit plus we have failed at least once, abort.
                    if (sw.ElapsedMilliseconds > maxWait && failCount >= minRetries)
                    {
                        throw new Exception($"Transient error did not go away after waiting for {maxWait}ms and failing {failCount} times...", e);
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
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static string DebugTable(DataTable table)
        {
            StringBuilder sb = new StringBuilder();

            int zeilen = table.Rows.Count;
            int spalten = table.Columns.Count;

            // Header
            for (int i = 0; i < table.Columns.Count; i++)
            {
                string s = table.Columns[i].ToString();
                sb.Append(string.Format("{0,-20} | ", s));
            }

            sb.Append(Environment.NewLine);
            for (int i = 0; i < table.Columns.Count; i++)
            {
                sb.Append("---------------------|-");
            }

            sb.Append(Environment.NewLine);

            // Data
            for (int i = 0; i < zeilen; i++)
            {
                DataRow row = table.Rows[i];
                
                // Debug.WriteLine("{0} {1} ", row[0], row[1]);
                for (int j = 0; j < spalten; j++)
                {
                    string s = row[j].ToString();
                    if (s.Length > 20)
                    {
                        s = s.Substring(0, 17) + "...";
                    }

                    sb.Append(string.Format("{0,-20} | ", s));
                }

                sb.Append(Environment.NewLine);
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                sb.Append("---------------------|-");
            }

            sb.Append(Environment.NewLine);

            return sb.ToString();
        }

        /// <summary>
        /// Moves a directory (MOVE) if in same drive, or copies and deletes if between drives
        /// as MOVE operation is not supported in such scenario. Supports long path names.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="logger"></param>
        /// <param name="ignoreOnDeployPattern"></param>
        public static void MoveDirectory(string source, string destination, ILoggerInterface logger, string ignoreOnDeployPattern = null)
        {
            try
            {
                source = EnsureLongPathSupportIfAvailable(source);
                destination = EnsureLongPathSupportIfAvailable(destination);

                RetryWhile(
                    () => { Directory.Move(source, destination); },

                    // Retry while access to the path is denied, in move operations
                    // this might happen due to files being scanned by an antivirus
                    // or other transient locks
                    (e) => Convert.ToString((uint)e.HResult) == "2147942405",
                    10000,
                    logger);
            }
            catch (IOException e)
            {
                if (e.HResult != -2146232800)
                {
                    throw;
                }

                logger.LogInfo(
                    true,
                    $"Move operation cannot complete because source '{source}' and destination '{destination}' are on same drive, falling back to copy.");

                CopyFilesRecursivelyFast(source, destination, false, ignoreOnDeployPattern, logger);
                Directory.Delete(source, true);
            }
        }

        /// <summary>
        /// Copy files beteween two directories
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreErrors"></param>
        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, bool overwrite = false, bool ignoreErrors = false)
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
        public static void CopyFilesRecursivelyFast(
            string source,
            string target,
            bool overwrite,
            string ignoreOnDeployPattern,
            ILoggerInterface logger)
        {
            source = EnsureLongPathSupportIfAvailable(source);
            target = EnsureLongPathSupportIfAvailable(target);

            DoCopyFilesRecursivelyFast(
                new DirectoryInfo(source),
                new DirectoryInfo(target),
                overwrite,
                ignoreOnDeployPattern,
                logger);
        }

        /// <summary>
        /// Copy files recursively.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="overwrite"></param>
        /// <param name="ignoreOnDeployPattern"></param>
        /// <param name="logger"></param>
        private static void DoCopyFilesRecursivelyFast(
            DirectoryInfo source,
            DirectoryInfo target,
            bool overwrite,
            string ignoreOnDeployPattern,
            ILoggerInterface logger)
        {
            var files = source.EnumerateFiles("*", SearchOption.AllDirectories);
            ParallelOptions pop = new ParallelOptions();

            // The bottle neck here is disk rather than CPU... but number of CPU's is a good measure
            // of how powerful the target machine might be...
            pop.MaxDegreeOfParallelism = (int)Math.Ceiling(Environment.ProcessorCount * 1.5);
            logger.LogInfo(true, "Copying files from {1} with {0} threads.", pop.MaxDegreeOfParallelism, source.FullName);

            var ignoreOnDeployRegex = string.IsNullOrWhiteSpace(ignoreOnDeployPattern)
                ? null
                : new Regex(ignoreOnDeployPattern);

            int fileCount = 0;
            int dirCount = 0;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.ForEach(files, pop, (i) =>
            {
                try
                {
                    var dir = i.Directory.FullName;

                    var relativeDir =
                        dir.Substring(source.FullName.Length, dir.Length - source.FullName.Length)
                            .TrimStart("\\".ToCharArray());

                    var relativeFile = Path.Combine(relativeDir, i.Name);

                    var destDir = Path.Combine(target.FullName, relativeDir);
                    var destFile = new FileInfo(Path.Combine(destDir, i.Name));

                    if (ignoreOnDeployRegex?.IsMatch(relativeFile) == true)
                    {
                        return;
                    }

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                        Interlocked.Add(ref dirCount, 1);
                    }

                    if ((!destFile.Exists) || (destFile.Exists && overwrite))
                    {
                        i.CopyTo(destFile.FullName, true);
                        Interlocked.Add(ref fileCount, 1);
                    }
                }
                catch (Exception e)
                {
                    // Reserved names cannot be copied...
                    if (InvalidWindowsFileNames.Contains(i.Name.ToLower()))
                    {
                        logger.LogWarning(true, "Skipped invalid file: " + i.FullName);
                    }
                    else
                    {
                        throw new Exception("Error copying file: " + i.FullName, e);
                    }
                }

                if (sw.ElapsedMilliseconds > 2000)
                {
                    lock (string.Intern("utils-system-filecopy-fast"))
                    {
                        if (sw.ElapsedMilliseconds > 2000)
                        {
                            try
                            {
                                int leftPos = Console.CursorLeft - 80;
                                Console.SetCursorPosition(leftPos >= 0 ? leftPos : 0, Console.CursorTop);
                                Console.Write($"Copied {fileCount} files.".PadRight(80, " ".ToCharArray().First()));
                            }
                            catch (IOException)
                            {
                                // ignored, the console might no always be available (i.e. in a service)
                                // Exception Type: 'System.IO.IOException
                                // ExceptionMessage: The handle is invalid.
                            }

                            sw.Restart();
                        }
                    }
                }
            });

            logger.LogInfo(true, "Copied {0} files and {1} directories.", fileCount, dirCount);
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

            logger.LogInfo(true, "Removing directory {0} with close processes {1}", strDir, closeProcesses == null ? string.Empty : string.Join(", ", closeProcesses));

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
                    DeleteDirectoryAndRemovePermissionsIfNeeded(strDir);
                },
                ExceptionIsAccessDeniedOrFileInUse,
                waitTimeIfInUse * 1000,
                logger);

            if (Directory.Exists(strDir))
            {
                throw new Exception($"Could not completely delete directory '{strDir}', see log for details.");
            }
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
        private static void DeleteDirectoryAndRemovePermissionsIfNeeded(string strDir)
        {
            ValidateDirectoryDepthDeletion(strDir);
            strDir = EnsureLongPathSupportIfAvailable(strDir);

            if (!Directory.Exists(strDir))
            {
                return;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? Environment.ProcessorCount - 1 : 1
            };

            DeleteJunctionsOrLinks(strDir);

            int exceptionBreakCount = 100;
            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.ForEach(
                Directory.EnumerateFiles(strDir, "*", SearchOption.AllDirectories),
                parallelOptions,
                (f, loopState) =>
                {
                    try
                    {
                        DeleteSingleFile(f);
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

            Parallel.ForEach(
                Directory.EnumerateDirectories(strDir, "*", SearchOption.AllDirectories),
                parallelOptions,
                (f, loopState) =>
                {
                    try
                    {
                        DeleteSingleDirectory(f);
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

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }

            DeleteSingleDirectory(strDir);
        }

        /// <summary>
        /// Clear top-down junctions or links in a directory structure
        /// </summary>
        /// <param name="directory"></param>
        private static void DeleteJunctionsOrLinks(string directory)
        {
            foreach (var dir in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (UtilsJunction.IsJunctionOrSymlink(dir))
                {
                    UtilsJunction.DeleteJunctionOrSymlink(dir);
                }
                else
                {
                    DeleteJunctionsOrLinks(dir);
                }
            }
        }

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
            return new Uri(path).IsUnc;
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
        /// Download the contents of a remote URL as plain text.
        /// * Does not fail on SSL validation errors (for self signed certificates, etc..)
        /// * Retries the download to deal with connection glitches
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="retry"></param>
        /// <param name="expectedCode"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static string DownloadUriAsText(
            string uri,
            bool retry = true,
            HttpStatusCode expectedCode = HttpStatusCode.OK,
            Dictionary<string, string> headers = null)
        {
            using (WebClient client = new WebClient())
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        client.Headers[header.Key] = header.Value;
                    }
                }

                // Change SSL checks so that all checks pass
                ServicePointManager.ServerCertificateValidationCallback =
                    new RemoteCertificateValidationCallback(
                        delegate { return true; });

                string response;

                try
                {
                    response = client.DownloadString(uri);
                }
                catch (WebException webException)
                {
                    if (webException.Response == null)
                    {
                        throw;
                    }

                    var rs = (HttpWebResponse)webException.Response;

                    response = new StreamReader(rs.GetResponseStream()).ReadToEnd();

                    if (rs.StatusCode != expectedCode)
                    {
                        throw new Exception(
                            $"Unexpected status code at {uri}. Received {rs.StatusCode} expected {expectedCode}");
                    }
                }
                catch
                {
                    if (retry == false)
                    {
                        throw;
                    }

                    // Sometimes requests made inmediately after an IIS
                    // reconfiguration can fail as it takes some time for
                    // IIS to materialize the changes
                    Thread.Sleep(500);
                    response = client.DownloadString(uri);
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
        /// If this is a valid SID
        /// </summary>
        /// <param name="sid"></param>
        /// <returns></returns>
        public static bool IsValidSid(string sid)
        {
            try
            {
                SecurityIdentifier s = new SecurityIdentifier(sid);
                return true;
            }
            catch (ArgumentException)
            {
                // Handle invalid SID
            }

            return false;
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
