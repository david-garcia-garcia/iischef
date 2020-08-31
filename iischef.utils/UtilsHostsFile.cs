using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace iischef.utils
{
    /// <summary>
    /// Utility class to manipulate the system's HOSTS file
    /// </summary>
    public class UtilsHostsFile
    {
        /// <summary>
        /// Devuelve la ubicación del fichero HOSTS
        /// </summary>
        private string HostsFile
        {
            get
            {
                return Environment.SystemDirectory + @"\drivers\etc\hosts";
            }
        }

        /// <summary>
        /// Devuelve el fichero de hosts como un conjunto de lineas
        /// </summary>
        /// <returns></returns>
        private List<string> GetHosts()
        {
            var content = System.IO.File.ReadAllText(this.HostsFile);
            return System.Text.RegularExpressions.Regex.Split(content, Environment.NewLine).ToList();
        }

        /// <summary>
        /// Añade un mapping al fichero hosts, o lo actualiza en función del hostname.
        /// Evita añadir duplicados o conflictivos.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="hostname"></param>
        public void AddHostsMapping(string address, string hostname)
        {
            var lines = this.GetHosts();
            var line = address + " " + hostname;
            var found = false;

            // First look for the HOST name
            for (int x = 0; x < lines.Count(); x++)
            {
                // Pasamos d elos comentarios.
                if (lines[x].Trim().StartsWith("#"))
                {
                    continue;
                }

                // Si no tiene exactamente dos partes, es que hay algo raro....
                var items = lines[x].Split(" ".ToCharArray());
                if (items.Count() != 2)
                {
                    continue;
                }

                // Lo que hace que dos entradas sean iguales es que tengan
                // el mismo hostname.
                if (string.Equals(items.Last(), hostname, StringComparison.InvariantCultureIgnoreCase))
                {
                    lines[x] = line;
                    found = true;
                    break;
                }
            }

            // If not found we need to add it
            if (!found)
            {
                lines.Add("# Automatic health monitoring hosts binding");
                lines.Add(line);
                File.WriteAllLines(this.HostsFile, lines, Encoding.Default);
            }
        }
    }
}
