using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Web;
using iischef.core.Configuration;
using Newtonsoft.Json.Linq;

namespace iischef.core
{
    /// <summary>
    /// Use this class to replace patterns from 
    /// configuration settings
    /// </summary>
    public class RuntimeSettingsReplacer
    {
        /// <summary>
        /// The settings.
        /// </summary>
        protected Dictionary<string, string> Settings;

        /// <summary>
        /// Nested settings
        /// </summary>
        protected JObject NestedSettings;

        /// <summary>
        /// Get an instance of RuntimeSettingsReplacer
        /// </summary>
        /// <param name="settings"></param>
        public RuntimeSettingsReplacer(Dictionary<string, string> settings)
        {
            this.Settings = settings;

            var settingsConverter = new JObjectToKeyValueConverter();
            this.NestedSettings = settingsConverter.keyValueToNested(settings);
        }

        /// <summary>
        /// Do the replacements...
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        public string DoReplace(string contents)
        {
            // Keys with full path
            foreach (var kvpsetting in this.Settings)
            {
                contents = contents.Replace("{" + kvpsetting.Key + "}", kvpsetting.Value);
            }

            // New support for filters and strong verification....
            // string a = "{@smykey.myvalue.thisisit|arg1-0, arg1-1|arg2@}";
            var matches = Regex.Matches(contents, "{@([^@]*)@}");
            foreach (Match match in matches)
            {
                string textMatch = match.Groups[1].Value;

                var parts = textMatch.Split("|".ToCharArray()).ToList();

                string replacementValue = string.Empty;

                if (parts.First().StartsWith("!"))
                {
                    string configKey = parts[0].Substring(1, parts[0].Length - 1);
                    var token = this.NestedSettings.SelectToken(configKey);
                    if (token != null)
                    {
                        replacementValue = token.ToString();
                    }
                }
                else
                {
                    // Primera parte es la variable de configuración y es obligatorio
                    string configKey = parts[0];
                    if (!this.Settings.ContainsKey(configKey))
                    {
                        throw new Exception(string.Format("Missing key in settings: " + configKey));
                    }

                    replacementValue = this.Settings[configKey];
                }

                parts.RemoveAt(0);

                foreach (string p in parts)
                {
                    var pos = p.IndexOf(":", StringComparison.Ordinal);
                    var operation = p.Substring(0, pos);
                    pos++;
                    string arguments = p.Substring(pos, p.Length - pos);
                    switch (operation)
                    {
                        case "filter":
                            replacementValue = this.OperationFilter(replacementValue, arguments);
                            break;
                        default:
                            throw new NotImplementedException("Operation not implemented: " + operation);
                    }
                }

                contents = contents.Replace(match.Value, replacementValue);
            }

            return contents;
        }

        /// <summary>
        /// Filters...
        /// </summary>
        /// <param name="input"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        protected string OperationFilter(string input, string arguments)
        {
            if (input == null)
            {
                return null;
            }

            var args = arguments.Split(",".ToCharArray()).Select((i) => i.Trim());

            foreach (string arg in args)
            {
                switch (arg)
                {
                    // Filtro de poner todo como "forwardslash"
                    case "allforward":
                        input = input?.Replace("\\", "/");
                        break;
                    case "trimpath":
                        input = input?.Trim(" \\//".ToCharArray());
                        break;
                    case "jsonescape":
                        input = HttpUtility.JavaScriptStringEncode(input);
                        break;
                    case "xmlescape":
                        input = SecurityElement.Escape(input);
                        break;
                    default:
                        throw new Exception("Filter not found: " + arg);
                }
            }

            return input;
        }
    }
}
