using iischef.logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace iischef.utils
{
    /// <summary>
    /// Utilities to deal with the hosts file
    /// </summary>
    public class UtilsHosts
    {
        /// <summary>
        /// Devuelve la ubicación del fichero HOSTS
        /// </summary>
        protected string HostsFile;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="hostsPath">The path to the hosts file, if null, the default system hosts file will be used.</param>
        public UtilsHosts(ILoggerInterface logger, string hostsPath = null)
        {
            this.HostsFile = hostsPath ?? Environment.SystemDirectory + @"\drivers\etc\hosts";
            this.Logger = logger;
        }

        /// <summary>
        /// Añade un mapping al fichero hosts, o lo actualiza en función del hostname.
        /// Evita añadir duplicados o conflictivos.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="hostname"></param>
        /// <param name="owner"></param>
        public void AddHostsMapping(string address, string hostname, string owner)
        {
            UtilsSystem.RetryWhile(() => this.DoAddHostsMapping(address, hostname, owner), (e) => e is IOException, 2000, this.Logger);
        }

        /// <summary>
        /// Remove a host mapping
        /// </summary>
        /// <param name="hostname">Use null to remove all host mappings for this application Id</param>
        /// <param name="owner"></param>
        public void RemoveHostsMapping(string owner, string hostname = null)
        {
            UtilsSystem.RetryWhile(() => this.DoRemoveHostsMapping(owner, hostname), (e) => e is IOException, 2000, this.Logger);
        }

        /// <summary>
        /// Find the line where the ownership group starts.
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="id"></param>
        /// <param name="createIfMissing"></param>
        protected int FindOwnerPosition(List<string> lines, string id, bool createIfMissing)
        {
            for (int x = 0; x < lines.Count(); x++)
            {
                // Pasamos de los comentarios.
                if (!lines[x].Trim().StartsWith("#chef"))
                {
                    continue;
                }

                // Si no tiene exactamente dos partes, es que hay algo raro....
                var siteIdMatch = Regex.Match(lines[x], "\\[(.*)]");

                if (!siteIdMatch.Success)
                {
                    continue;
                }

                var siteId = siteIdMatch.Groups[1].Value;

                if (siteId == id)
                {
                    return x;
                }
            }

            if (createIfMissing)
            {
                lines.Add("#chef Host Binding [" + id + "]");
                return lines.Count - 1;
            }

            return -1;
        }

        /// <summary>
        /// Añade un mapping al fichero hosts, o lo actualiza en función del hostname.
        /// Evita añadir duplicados o conflictivos.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="hostname"></param>
        /// <param name="owner"></param>
        protected void DoAddHostsMapping(string address, string hostname, string owner)
        {
            if (hostname.StartsWith("*"))
            {
                this.Logger.LogInfo(true, "Hosts file does not support wildcard mappings. Cannot add hostname: '{0}'", hostname);
                return;
            }

            var lines = this.ReadHostsFile();
            var line = address + " " + hostname;
            var found = false;

            int groupStart = this.FindOwnerPosition(lines, owner, true);

            for (int x = groupStart + 1; x < lines.Count(); x++)
            {
                // If we find a comment, it's the end of the group!
                if (lines[x].Trim().StartsWith("#"))
                {
                    break;
                }

                // Si no tiene exactamente dos partes, es que hay algo raro....
                var items = lines[x].Split(" ".ToCharArray());
                if (items.Count() != 2)
                {
                    continue;
                }

                // Lo que hace que dos entradas sean iguales es que tengan
                // el mismo hostname.
                if (string.Equals(items.Last(), hostname, StringComparison.InvariantCultureIgnoreCase) && string.Equals(items.First(), address, StringComparison.InvariantCultureIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                lines.Insert(groupStart + 1, line);
                this.CleanEmptySpaces(lines);
                File.WriteAllLines(this.HostsFile, lines, Encoding.Default);
                this.Logger.LogInfo(true, "Added HOSTS binding {0} for owner {1}", line, owner);
            }
        }

        /// <summary>
        /// Remove a host mapping
        /// </summary>
        /// <param name="hostname">Use null to remove all host mappings for this application Id</param>
        /// <param name="owner"></param>
        protected void DoRemoveHostsMapping(string owner, string hostname = null)
        {
            var lines = this.ReadHostsFile();
            var modified = false;

            int groupStart = this.FindOwnerPosition(lines, owner, false);

            // We only take care of bindings we own
            while (groupStart != -1)
            {
                for (int x = groupStart + 1; x < lines.Count(); x++)
                {
                    // Break, this means next group starts.
                    if (lines[x].Trim().StartsWith("#"))
                    {
                        break;
                    }

                    // Check real binding
                    var items = lines[x].Split(" ".ToCharArray());
                    if (items.Count() != 2)
                    {
                        continue;
                    }

                    // Verificar hostname matching
                    if (hostname != null &&
                        !string.Equals(items.Last(), hostname, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Si hemos llegado aquí es que hay que borrar... quitamos
                    // las dos líneas
                    lines.RemoveAt(x);
                    x = x - 1;

                    modified = true;
                }

                if (hostname == null)
                {
                    lines.RemoveAt(groupStart);
                    modified = true;
                }

                groupStart = this.FindOwnerPosition(lines, owner, false);
            }

            if (modified)
            {
                this.CleanEmptySpaces(lines);
                File.WriteAllLines(this.HostsFile, lines, Encoding.Default);
            }
        }

        /// <summary>
        /// Cleanup double spacing in hosts files
        /// </summary>
        /// <param name="lines"></param>
        protected void CleanEmptySpaces(List<string> lines)
        {
            for (int x = 0; x < lines.Count; x++)
            {
                // Do not process the last line
                if (x == lines.Count - 1)
                {
                    break;
                }

                // Pasamos de los comentarios.
                if (string.IsNullOrWhiteSpace(lines[x])
                 && string.IsNullOrWhiteSpace(lines[x + 1]))
                {
                    lines.RemoveAt(x);
                    x--;
                }
            }
        }

        /// <summary>
        /// Devuelve el fichero de hosts como un conjunto de lineas
        /// </summary>
        /// <returns></returns>
        protected List<string> ReadHostsFile()
        {
            var content = File.ReadAllText(this.HostsFile);
            return Regex.Split(content, Environment.NewLine).ToList();
        }
    }
}
