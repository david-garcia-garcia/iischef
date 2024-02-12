using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace iischef.utils
{
    public delegate void Eventhandler(object sender, string line, string stream);

    /// <summary>
    /// Esto es para llamar a consola directamente (no powershell)
    /// </summary>
    public class ConsoleCommand : IDisposable
    {
        public event Eventhandler LineAdded;

        private Process Process;

        private StreamReaderLineInput reader;

        private CancellationTokenWrapper CancellationToken;

        private Dictionary<string, StringBuilder> OuputStreamsCopy;

        public ConsoleCommand(
            string domain = null,
            string username = null,
            string password = null,
            string verb = null,
            bool useShellEsecute = false,
            CancellationTokenWrapper cancellationToken = null,
            bool storeStreamCopy = false)
        {
            if (storeStreamCopy || UnitTestDetector.IsRunningInTests)
            {
                this.OuputStreamsCopy = new Dictionary<string, StringBuilder>();
            }

            this.CancellationToken = cancellationToken;

            this.Process = new Process();

            // Configuramos el proceso.
            this.Process.StartInfo.FileName = "cmd.exe";
            this.Process.StartInfo.CreateNoWindow = true;
            this.Process.StartInfo.Arguments = "/k";

            this.Process.StartInfo.RedirectStandardInput = true;
            this.Process.StartInfo.RedirectStandardOutput = true;
            this.Process.StartInfo.RedirectStandardError = true;

            this.Process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            this.Process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            this.Process.StartInfo.UseShellExecute = useShellEsecute;
            this.Process.StartInfo.ErrorDialog = false;

            this.Process.StartInfo.Domain = domain;
            this.Process.StartInfo.UserName = username;
            this.Process.StartInfo.LoadUserProfile = true;
            this.Process.StartInfo.PasswordInClearText = password;

            this.Process.StartInfo.Verb = verb;

            this.Process.EnableRaisingEvents = true;
            this.Process.Start();

            // El problema de leer esto en dos threads separados
            // es que se pueden solapar mensajes que deberían ir juntos!!
            // lo que nos obliga a buscar periodos de inactividad para
            // mostrar los errores.
            this.reader = new StreamReaderLineInput((stream, content) => this.AddLine(content, stream), cancellationToken);
            
            this.reader.AddReader("error", this.Process.StandardError);
            this.reader.AddReader("output", this.Process.StandardOutput);

            this.reader.StartReading();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        public static string EscapeForArgument(string argument)
        {
            argument = argument.Replace("\"", "\"\"");
            return "\"" + argument + "\"";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        public int RunCommandAndWait(string command, out string error)
        {
            error = null;

            this.CancellationToken?.ThrowIfCancellationRequested();

            // https://ss64.com/nt/errorlevel.html
            command += Environment.NewLine + $"(echo finished:%ERRORLEVEL% & echo finished:%ERRORLEVEL% >&2)";

            StringBuilder errorMessage = new StringBuilder();

            Dictionary<string, bool> finished = new Dictionary<string, bool> { { "output", false }, { "error", false } };
            
            // Default to -2 exist codes, which actually means an error
            Dictionary<string, int> exitCodes = new Dictionary<string, int> { { "output", -22 }, { "error", -22 } };

            this.LineAdded += (object sender, string line, string stream) =>
            {
                var cleanLine = line
                    .Replace(((char)32).ToString(), string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty);

                // If this is a clear-console line, sleep a little
                if (cleanLine == string.Empty)
                {
                    Thread.Sleep(150);
                }

                this.HandleStream(stream, cleanLine, line, ref finished, ref exitCodes);

                if (!finished["error"] && stream == "error")
                {
                    errorMessage.AppendLine(cleanLine);
                }
            };

            this.RunCommand(command);

            while (true)
            {
                // If the internal thread is not reading anymore, stop the loop
                if (!this.reader.Reading)
                {
                    break;
                }

                // If both streams are finished, stop the loop
                if (finished["output"] && finished["error"])
                {
                    break;
                }

                Thread.Sleep(50);

                if (this.CancellationToken?.IsCancellationRequested == true)
                {
                    this.EnsureProcessProperlyClosed();
                    this.CancellationToken?.ThrowIfCancellationRequested();
                }
            }

            if (this.CancellationToken?.IsCancellationRequested == true)
            {
                this.EnsureProcessProperlyClosed();
                this.CancellationToken?.ThrowIfCancellationRequested();
            }

            error = errorMessage.ToString();

            return exitCodes["output"];
        }

        private void HandleStream(string stream, string cleanLine, string line, ref Dictionary<string, bool> finished, ref Dictionary<string, int> exitCodes)
        {
            var match = Regex.Match(cleanLine, "^finished:(.*)");

            if (match.Success)
            {
                if (match.Groups.Count > 1)
                {
                    int.TryParse(match.Groups[1].Value, out var exitCode);
                    exitCodes[stream] = exitCode;
                }

                finished[stream] = true;
            }
            else
            {
                if (this.OuputStreamsCopy != null)
                {
                    if (!this.OuputStreamsCopy.ContainsKey(stream))
                    {
                        this.OuputStreamsCopy[stream] = new StringBuilder();
                    }

                    this.OuputStreamsCopy[stream].AppendLine(line);
                }

                Console.Out.Write(stream + ":" + line);
            }
        }

        public void RunCommand(string cmd)
        {
            this._RunCommand(cmd);
        }

        private void _RunCommand(string cmd)
        {
            this._SendInput(cmd);
        }

        /// <summary>
        /// El output de consola se va añadiendo
        /// caracter a caracter, pero luego se hace un post-procesado.
        /// </summary>
        private void AddLine(string output, string stream)
        {
            lock (string.Intern("reading-buffer"))
            {
                this.LineAdded?.Invoke(this, output, stream);
            }
        }

        private void _SendInput(string input)
        {
            this.Process.StandardInput.Flush();
            this.Process.StandardInput.WriteLineAsync(input);
        }

        public void Dispose()
        {
            this.EnsureProcessProperlyClosed();
        }

        private void EnsureProcessProperlyClosed()
        {
            // Close the console process, if still opened.
            try
            {
                this.reader.StopReading();
            }
            catch
            {
                // ignored
            }

            if (this.Process == null)
            {
                return;
            }

            var process = this.Process;
            this.Process = null;

            // Make sure we reset the return code..
            try
            {
                process.StandardInput.WriteLine("exit 0");
            }
            catch
            {
                // ignored
            }

            if (!process.WaitForExit(1500))
            {
                // Use 'taskkill' to kill the entire process tree.
                var taskKillProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {process.Id}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                taskKillProcess.Start();
                taskKillProcess.WaitForExit();
            }

            process.Close();
            process.Dispose();
        }
    }
}
