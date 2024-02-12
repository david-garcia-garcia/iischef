using iischef.logger;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace iischef.utils
{
    public static class UtilsProcess
    {
        /// <summary>
        /// Devuelve un listado con todos los procesos que tienen
        /// algun fichero bloqueado de manera directa en el path 
        /// del parámetro.
        /// </summary>
        /// <param name="processes"></param>
        /// <returns></returns>
        public static List<Process> GetProcessInstance(List<ProcessInfo> processes)
        {
            ConcurrentBag<Process> myProcessArray = new ConcurrentBag<Process>();

            List<Process> processlist = Process.GetProcesses().ToList();

            var ops = new ParallelOptions();
            ops.MaxDegreeOfParallelism = 3;

            Parallel.ForEach(processlist, ops, (myProcess, loopState) =>
            {
                if (myProcessArray.Count == processes.Count)
                {
                    loopState.Break();
                    return;
                }

                // There are randmo exceptions such as the process exited
                // between the listing and actually operating on it.
                try
                {
                    if (processes.Any((i) => i.ProcessId == myProcess.Id))
                    {
                        myProcessArray.Add(myProcess);
                        return;
                    }

                    // if (!myProcess.HasExited) //This will cause an "Access is denied" error
                    // if (myProcess.Threads.Count > 0)
                    // {
                    //     try
                    //     {
                    //         ProcessModuleCollection modules = myProcess.Modules;
                    //         for (var j = 0; j <= modules.Count; j++)
                    //         {
                    //             if ((modules[j].FileName.ToLower().IndexOf(path.ToLower()) != -1))
                    //             {
                    //                 myProcessArray.Add(myProcess);
                    //                 return;
                    //             }
                    //         }
                    //     }
                    //     catch (Exception exception)
                    //     {
                    //         //MsgBox(("Error : " & exception.Message)) 
                    //     }
                    // }
                }
                catch (Exception e)
                {
                    return;
                }
            });

            return myProcessArray.ToList();
        }

        /// <summary>
        /// Get the executable path of a process from it's process id
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public static ProcessInfo GetProcessInfo(int processId, ILoggerInterface logger)
        {
            ProcessInfo result = new ProcessInfo();
            result.ProcessId = processId;

            try
            {
                string query = "SELECT ExecutablePath, Name, CommandLine FROM Win32_Process WHERE ProcessId = " + processId;

                using (ManagementObjectSearcher mos = new ManagementObjectSearcher(query))
                {
                    using (ManagementObjectCollection moc = mos.Get())
                    {
                        result.MainModulePath = (from mo in moc.Cast<ManagementObject>() select mo["ExecutablePath"]).First().ToString();
                        result.ProcessName = (from mo in moc.Cast<ManagementObject>() select mo["Name"]).First().ToString();
                        result.CommandLine = (from mo in moc.Cast<ManagementObject>() select mo["CommandLine"]).First().ToString();
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(true, e.Message);
            }

            return result;
        }

        public class ProcessInfo
        {
            public int ProcessId { get; set; }

            public string ProcessName { get; set; }

            public string MachineName { get; set; }

            public string MainModulePath { get; set; }

            public string CommandLine { get; set; }
        }

        public static List<ProcessInfo> GetPathProcessesInfo(string path, ILoggerInterface logger, bool logDetails = false)
        {
            List<ProcessInfo> result = new List<ProcessInfo>();

            foreach (var process in GetProcessesThatBlockPathHandle(path, logger, logDetails))
            {
                try
                {
                    result.Add(GetProcessInfo(process.pid, logger));
                }
                catch (Exception e)
                {
                    logger.LogException(e, EventLogEntryType.Warning);
                }
            }

            return result;
        }

        /// <summary>
        /// Closes all the handles that block any files in the specified path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="allowedProcesses">List of whitelisted processes</param>
        /// <param name="logger"></param>
        public static void ClosePathProcesses(
            string path,
            List<string> allowedProcesses,
            ILoggerInterface logger)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Make sure the path exists
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            // Load list of processes that block directory
            var processes = GetPathProcessesInfo(path, logger);

            // Filter the whitelisted
            string regex = string.Join("|", allowedProcesses);
            var processesThatWillBeClosed = processes.Where((i) => i.MainModulePath != null && Regex.IsMatch(i.MainModulePath, regex)).ToList();

            if (!processesThatWillBeClosed.Any())
            {
                return;
            }

            // Message of processes that will not be closed
            var processesThatWillNotBeClosed = processes.Except(processesThatWillBeClosed).ToList();
            if (processesThatWillNotBeClosed.Any())
            {
                logger.LogWarning(true, "The following processes are not whitelisted and will not be closed {0}", string.Join(", ", processesThatWillNotBeClosed.Select((i) => i.ProcessName)));
            }

            // Grab the actual process instances
            var processesInstances = GetProcessInstance(processesThatWillBeClosed);

            // First kill al the processes.
            foreach (var p in processesInstances)
            {
                try
                {
                    logger.LogInfo(true, "Killing process: {0}", p.ProcessName);

                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                catch (Exception e)
                {
                    logger.LogException(e, EventLogEntryType.Warning);
                }
            }

            // Even though the processes have exited, handles take a while to be released
            Thread.Sleep(500);

            foreach (var p in processesInstances)
            {
                bool hasClosed = UtilsSystem.WaitWhile(() => !p.HasExited, 15000, $"Waiting for process {p.ProcessName} to close.", logger);
                logger.LogInfo(true, "Process {0} has closed: {1}", p.ProcessName, hasClosed);
            }
        }

        /// <summary>
        /// Handle info
        /// </summary>
        public class Handle
        {
            /// <summary>
            /// The process Id
            /// </summary>
            public int pid { get; set; }
        }

        private static List<Handle> GetProcessesThatBlockPathHandle(string path, ILoggerInterface logger, bool logDetails = false)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return new List<Handle>();
            }

            string key = "SOFTWARE\\Sysinternals\\Handle";
            string name = "EulaAccepted";

            // This Utility has an EULA GUI on first run... try to avoid that
            // by manually setting the registry
            int? eulaaccepted64 = (int?)UtilsRegistry.GetRegistryKeyValue64(RegistryHive.CurrentUser, key, name, null);
            int? eulaaccepted32 = (int?)UtilsRegistry.GetRegistryKeyValue32(RegistryHive.CurrentUser, key, name, null);

            bool eulaaccepted = (eulaaccepted32 == 1 && eulaaccepted64 == 1);

            if (!eulaaccepted)
            {
                UtilsRegistry.SetRegistryValue(RegistryHive.CurrentUser, key, name, 1, RegistryValueKind.DWord);
            }

            // Normalize the path, to ensure that long path is not used, otherwise handle.exe won't work as expected
            string fileName = UtilsSystem.RemoveLongPathSupport(path);

            List<Handle> result = new List<Handle>();
            string outputTool = string.Empty;

            // Gather the handle.exe from the embeded resource and into a temp file
            var handleexe = UtilsSystem.GetTempPath("handle") + Guid.NewGuid().ToString().Replace("-", "_") + ".exe";
            UtilsSystem.EmbededResourceToFile(Assembly.GetExecutingAssembly(), "_Resources.Handle.exe", handleexe);

            try
            {
                using (Process tool = new Process())
                {
                    tool.StartInfo.FileName = handleexe;
                    tool.StartInfo.Arguments = fileName;
                    tool.StartInfo.UseShellExecute = false;
                    tool.StartInfo.Verb = "runas";
                    tool.StartInfo.RedirectStandardOutput = true;
                    tool.Start();

                    int timeoutMs = 3000;

                    Task<string> readTask = tool.StandardOutput.ReadToEndAsync();

                    if (Task.WhenAny(readTask, Task.Delay(timeoutMs)).Result == readTask)
                    {
                        // The read task completed before the timeout
                        outputTool = readTask.Result;
                    }
                    else
                    {
                        // The read task did not complete before the timeout
                        logger.LogWarning(false, "_Resources.Handle.exe was unable to terminate in appropiate amount of time.");
                    }

                    try
                    {
                        tool.WaitForExit(timeoutMs);
                    }
                    finally
                    {
                        if (!tool.HasExited)
                        {
                            tool.Kill();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogException(e, EventLogEntryType.Warning);
            }
            finally
            {
                UtilsSystem.DeleteFile(handleexe, logger, 5);
            }

            string matchPattern = @"(?<=\s+pid:\s+)\b(\d+)\b(?=\s+)";
            foreach (Match match in Regex.Matches(outputTool, matchPattern))
            {
                if (int.TryParse(match.Value, out var pid))
                {
                    if (result.All(i => i.pid != pid))
                    {
                        result.Add(new Handle()
                        {
                            pid = pid
                        });
                    }
                }
            }

            if (result.Any() && logDetails)
            {
                logger?.LogInfo(true, outputTool);
            }

            return result;
        }
    }
}
