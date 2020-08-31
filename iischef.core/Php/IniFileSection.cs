using System.Collections.Generic;

namespace iischef.core.Php
{
    public class IniFileSection
    {
        public bool IsCommented 
        { 
            get { return this._IsCommented; } 
        }

        public IniFileSection(string name, bool isCommented)
        {
            this._IsCommented = isCommented;
            this.name = name;
        }

        public string Name 
        { 
            get { return this.name; } 
        }

        private string name;
#pragma warning disable SA1309 // Field names should not begin with underscore
        private bool _IsCommented;
#pragma warning restore SA1309 // Field names should not begin with underscore

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        public List<IniFileLine> lines = new List<IniFileLine>();
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }
}
