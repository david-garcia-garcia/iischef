using System;
using System.Collections.Generic;

namespace iischef.core.Php
{
    public class IniFileNamespace
    {
        public IniFileNamespace(string value)
        {
            this.Hostname = value;
        }

        /// <summary>
        /// Valor del namespace.
        /// </summary>
        public string Hostname { get; set; }

        public Dictionary<string, IniFileSection> Sections = new Dictionary<string, IniFileSection>(StringComparer.InvariantCultureIgnoreCase);
    }
}
