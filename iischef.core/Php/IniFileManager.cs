using iischef.logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace iischef.core.Php
{
    /// <summary>
    /// Tool to manipulate INI files
    /// </summary>
    public class IniFileManager
    {
        public const string CST_SEC_GLOBAL = "GLOBALSECTION-----";

        /// <summary>
        /// Ini file location.
        /// </summary>
        protected string IniFilePath;

        /// <summary>
        /// Logger
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// 
        /// </summary>
        protected List<IniFileNamespace> Namespaces = new List<IniFileNamespace>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nameSpace"></param>
        /// <returns></returns>
        public Dictionary<string, IniFileSection> GetSections(string nameSpace = null)
        {
            foreach (var oSpace in this.Namespaces)
            {
                if (oSpace.Hostname == nameSpace)
                {
                    return oSpace.Sections;
                }
            }

            this.Namespaces.Add(new IniFileNamespace(nameSpace));

            return this.Namespaces.Where((i) => i.Hostname == nameSpace).Single().Sections;
        }

        public IniFileManager(string filename, ILoggerInterface logger)
        {
            this.Logger = logger;
            this.IniFilePath = filename;
            this.Parse();
        }

        private void Parse()
        {
            var lines = System.IO.File.ReadAllLines(this.IniFilePath);

            IniFileNamespace currentNamespace;
            IniFileSection currentSection;

            currentNamespace = new IniFileNamespace(null);
            this.Namespaces.Add(currentNamespace);

            currentSection = new IniFileSection(CST_SEC_GLOBAL, false);
            currentNamespace.Sections.Add(currentSection.Name, currentSection);

            foreach (var line in lines)
            {
                var oLine = line.Trim();

                if (oLine.Trim().ToLower().StartsWith("[host="))
                {
                    var nspace = oLine.Substring(6, oLine.Length - 6).Trim("[] ".ToCharArray());
                    currentNamespace = new IniFileNamespace(nspace);
                    this.Namespaces.Add(currentNamespace);

                    currentSection = new IniFileSection(CST_SEC_GLOBAL, false);
                    currentNamespace.Sections.Add(currentSection.Name, currentSection);

                    continue;
                }

                // Caso secciones
                if (oLine.TrimStart("; ".ToCharArray()).StartsWith("[") && oLine.Contains("]"))
                {
                    // Cambio de sección
                    string sectionName = oLine.Trim("[];".ToCharArray());
                    if (!currentNamespace.Sections.ContainsKey(sectionName))
                    {
                        currentSection = new IniFileSection(sectionName, oLine.StartsWith(";"));
                        currentNamespace.Sections.Add(currentSection.Name, currentSection);
                        continue;
                    }
                }
                
                // Caso de directiva o comentario (parseamos algunos comentarios como directiva para poder comentar/descomentar)
                else
                {
                    currentSection.lines.Add(new IniFileLine(line, currentSection.Name));
                }
            }
        }

        /// <summary>
        /// Guarda los cambios!
        /// </summary>
        public void Save()
        {
            List<string> resultLines = new List<string>();
            foreach (var nspace in this.Namespaces)
            {
                if (nspace.Hostname != null)
                {
                    var l = $"[HOST={nspace.Hostname}]";
                    resultLines.Add(l);
                }

                foreach (var section in nspace.Sections)
                {
                    var hasRealData = section.Value.lines.Where((i) => i.Type == IniFileLineType.Directive
                        && i.IsCommented == false).Any();

                    if (section.Key != CST_SEC_GLOBAL)
                    {
                        var l = string.Format("[{0}]", section.Key);

                        // Si estaba comentada, la dejamos como estaba....
                        if (section.Value.IsCommented)
                        {
                            l = ";" + l;
                        }

                        resultLines.Add(l);
                    }

                    foreach (var line in section.Value.lines)
                    {
                        if (line.Type == IniFileLineType.Blank)
                        {
                            resultLines.Add(null);
                            continue;
                        }
                        else if (line.Type == IniFileLineType.Comment)
                        {
                            resultLines.Add(string.Format(";{0}", line.CommentBody));
                            continue;
                        }
                        else if (line.Type == IniFileLineType.Directive)
                        {
                            resultLines.Add(line.render());
                        }
                    }
                }
            }

            System.IO.File.WriteAllLines(this.IniFilePath, resultLines);
            this.Logger.LogInfo(true, "PHP ini settings written to {0}", this.IniFilePath);
        }

        /// <summary>
        /// Enumeración de todos las líneas en un nameSpace concreto.
        /// Usar nullo para el nameSpace global.
        /// </summary>
        /// <param name="namespace"></param>
        /// <returns></returns>
        private IEnumerable<IniFileLine> GetAllLines(string nameSpace = null)
        {
            var s = this.GetSections(nameSpace);
            return s.SelectMany((c) => c.Value.lines);
        }

        /// <summary>
        /// Write a specific directive to the log
        /// </summary>
        /// <param name="key"></param>
        /// <param name="nameSpace"></param>
        public void LogDirective(string key, string nameSpace = null)
        {
            var lines = (from p in this.GetAllLines(nameSpace)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase)
                         select p);

            foreach (var l in lines)
            {
                this.Logger.LogInfo(false, l.render());
            }
        }

        public void LogActiveDirectives(string nameSpace = null, string group = null)
        {
            var lines = (from p in this.GetAllLines(nameSpace)
                         where
                         p.Type == IniFileLineType.Directive &&
                         !p.IsCommented &&
                         (@group == null ? true : p.Key.StartsWith(@group))
                         select p);

            if (!lines.Any())
            {
                this.Logger.LogWarning(false, "The nameSpace '{0}' has no custom directives", nameSpace);
                return;
            }

            foreach (var l in lines)
            {
                this.Logger.LogInfo(false, l.render());
            }

            this.Logger.LogInfo(false, "{0} Directives Rendered", lines.Count().ToString());
        }

        /// <summary>
        /// http://php.net/manual/es/ini.sections.php
        /// </summary>
        private List<string> DirectivesNotWorkingOnHostnameSections = new List<string>() 
        {
            "extension",
            "zend_extension"
        };

        /// <summary>
        /// Comenta un grupo de directivas para todos los HOSTS
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="defaultSection"></param>
        public void CommentDirectiveGroup(string prefix)
        {
            var lines = (from p in this.GetAllLines(null)
                         where
                         p.Type == IniFileLineType.Directive &&
                         p.Key.ToLower().StartsWith(prefix.ToLower())
                         select p);

            foreach (var l in lines)
            {
                l.IsCommented = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">Clave</param>
        /// <param name="value">Valor</param>
        /// <param name="defaultSection">Nombre de la sección en la que se creará la directiva si no existe.</param>
        /// <param name="isCommented">Si la directiva debe estar o no comentada.</param>
        /// <param name="hostname">Si usas hostname, la seccion "default section" será ignorada.</param>
        public void UpdateOrCreateDirective(string key, string value, string defaultSection = CST_SEC_GLOBAL, bool isCommented = false, string hostname = null)
        {
            if (!string.IsNullOrWhiteSpace(hostname) && this.DirectivesNotWorkingOnHostnameSections.Contains(key))
            {
                throw new Exception($"Directiva {key} no válida en sección HOST={hostname}");
            }

            // logger.LogInfo(true, "[{3}] PHP.ini: {2}{0} = {1}", key, value, isCommented ? "; " : "", defaultSection);

            IniFileLine line = null;

            // Todas las posibles lineas, pueden haber directivas duplicadas y alguna de ellas comentada...
            var lines = (from p in this.GetAllLines(hostname)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase)
                         select p);

            var sections = this.GetSections(hostname);

            // Comentar todas las de otras secciones
            foreach (var l in lines)
            {
                if (!string.Equals(l.Section, defaultSection, StringComparison.CurrentCultureIgnoreCase))
                {
                    l.IsCommented = true;
                }
                else
                {
                    line = l;
                }
            }

            // La última no comentada.
            var uncommented = lines.Where((i) => i.IsCommented == false);

            // Preventively comment all, and keep only one
            foreach (var u in uncommented)
            {
                u.IsCommented = true;
                line = u;
            }

            // Only consider lines in my own section...
            if (line == null && lines.Any())
            {
                foreach (var l in lines)
                {
                    if (string.Equals(l.Section, defaultSection, StringComparison.CurrentCultureIgnoreCase))
                    {
                        line = l;
                        break;
                    }
                }
            }

            // Esto es que no hay nada de nada....
            if (line == null)
            {
                if (!sections.ContainsKey(defaultSection))
                {
                    sections.Add(defaultSection, new IniFileSection(defaultSection, false));
                }

                sections[defaultSection].lines.Add(new IniFileLine(key, value, defaultSection, false));
            }
            else
            {
                line.IsCommented = isCommented;
                line.Value = value;
            }
        }

        public string GetValue(string key, string hostname = null)
        {
            // Todas las posibles lineas, pueden haber directivas duplicadas y alguna de ellas comentada...
            var lines = (from p in this.GetAllLines(hostname)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase) &&
                         p.IsCommented == false
                         select p).ToList();

            if (!lines.Any())
            {
                return null;
            }

            return lines.First().Value;
        }

        public bool CheckDirectiveKeyValue(string key, string value, string hostname = null)
        {
            // Todas las posibles lineas, pueden haber directivas duplicadas y alguna de ellas comentada...
            var lines = (from p in this.GetAllLines(hostname)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase) &&
                         string.Equals(value, p.Value, StringComparison.CurrentCultureIgnoreCase) &&
                         p.IsCommented == false
                         select p);

            if (!lines.Any())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Busca una directiva en el INI, si no la encuentra la crea
        /// en la sección por defecto que se pase. Se usa para directivas multivalor
        /// como las extensiones!
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public void UpdateOrCreateMultivalueDirective(string key, string value, string defaultSection, bool isCommented = false, string hostname = null)
        {
            if (!string.IsNullOrWhiteSpace(hostname) && this.DirectivesNotWorkingOnHostnameSections.Contains(key))
            {
                throw new Exception(string.Format("Directiva {0} no válida en sección HOST={1}", key, hostname));
            }

            // this.Logger.LogInfo(true, "[{3}] PHP.ini: {2}{0} = {1}", key, value, isCommented ? "; " : "", defaultSection);

            IniFileLine line = null;

            // Todas las posibles lineas, pueden haber directivas duplicadas y alguna de ellas comentada...
            var lines = (from p in this.GetAllLines(hostname)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase) &&
                         string.Equals(value, p.Value, StringComparison.CurrentCultureIgnoreCase)
                         select p);

            // La última no comentada.
            var uncommented = lines.Where((i) => i.IsCommented == false);
            if (uncommented.Any())
            {
                line = uncommented.Last();
            }

            if (line == null && lines.Any())
            {
                // La última
                line = lines.Last();
            }

            var sections = this.GetSections(hostname);

            // Esto es que no hay nada de nada....
            if (line == null)
            {
                if (!sections.ContainsKey(defaultSection))
                {
                    sections.Add(defaultSection, new IniFileSection(defaultSection, false));
                }

                sections[defaultSection].lines.Add(new IniFileLine(key, value, defaultSection, false));
            }
            else
            {
                // Solo hay que descomentar tanto key como value son iguales!!
                line.IsCommented = isCommented;
            }
        }

        /// <summary>
        /// Comenta una directiva multivalor a partir del un regex contra el value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="regex"></param>
        /// <param name="defaultSection"></param>
        /// <param name="hostname"></param>
        public void CommentMultiValueDirective(string key, string regex, string defaultSection, string hostname = null)
        {
            if (!string.IsNullOrWhiteSpace(hostname) && this.DirectivesNotWorkingOnHostnameSections.Contains(key))
            {
                throw new Exception(string.Format("Directiva {0} no válida en sección HOST={1}", key, hostname));
            }

            System.Text.RegularExpressions.Regex r = new Regex(regex);

            // Todas las posibles lineas, pueden haber directivas duplicadas y alguna de ellas comentada...
            var lines = (from p in this.GetAllLines(hostname)
                         where
                         p.Type == IniFileLineType.Directive &&
                         string.Equals(key, p.Key, StringComparison.CurrentCultureIgnoreCase) &&
                         r.Matches(p.Value).Count > 0
                         select p);

            // Comentarlas!
            foreach (var l in lines)
            {
                l.IsCommented = true;
            }
        }
    }
}
