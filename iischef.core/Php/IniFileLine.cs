using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace iischef.core.Php
{
    public class IniFileLine
    {
        public static bool ValidKey(string key)
        {
            List<string> badChars = new List<string>() 
            {
                    "/",
                    "\\",
                    "[",
                    "]",
                    ":",
                    "\r",
                    "\n",
                    "#",
                    ";",
                    "=",
                    ":",
            };

            // (a single backslash, escaping the escape character)
            // \0 Null character
            // \a Bell/Alert/Audible
            // \b Backspace, Bell character for some applications
            // \t Tab character
            // \r Carriage return
            // \n Line feed
            // \; Semicolon
            // \# Number sign
            // \= Equals sign
            // \: Colon
            // \x????

            foreach (var s in badChars)
            {
                if (key.Contains(s))
                {
                    return false;
                }
            }

            return true;
        }

        public string render()
        {
            var l = string.Format(this._Template, this._Key, this._Value);
            if (this.IsCommented)
            {
                l = ";" + l;
            }

            return l;
        }

        // Queremos que los INI originales sean manipulados lo menos posible,
        // por eso recuperaremos templates del uso original.
        // Por defecto igual sin espacios.
#pragma warning disable SA1309 // Field names should not begin with underscore
        private string _Template = "{0}={1}";
#pragma warning restore SA1309 // Field names should not begin with underscore

        public string Key
        {
            get
            {
                return this._Key;
            }

            set
            {
                if (this._Type != IniFileLineType.Directive)
                {
                    throw new Exception("Cannot update key of a non directive line.");
                }

                if (!ValidKey(value))
                {
                    throw new Exception(string.Format("El valor de clave {0} tiene carácteres no admitidos.", value));
                }

                this._Key = value;
            }
        }

        public string Value
        {
            get
            {
                return this._Value;
            }
            set
            {
                if (this._Type != IniFileLineType.Directive)
                {
                    throw new Exception("Cannot update value of a non directive line.");
                }

                this._Value = value;
            }
        }

        public string CommentBody 
        { 
            get { return this._CommentBody; } 
        }

        public bool IsCommented
        {
            get
            {
                return this._IsCommented;
            }

            set
            {
                if (this._Type == IniFileLineType.Blank)
                {
                    throw new Exception("Blank Lines cannot be commented.");
                }

                this._IsCommented = value;
            }
        }

        public string Section
        {
            get
            {
                return this._Section;
            }
        }

        public IniFileLineType Type
        {
            get { return this._Type; }
        }
#pragma warning disable SA1309 // Field names should not begin with underscore
        private string _Key = null;
        private string _Value = null;
        private string _CommentBody = null;
        private bool _IsCommented = false;
        private string _Section = null;
        private IniFileLineType _Type;
#pragma warning restore SA1309 // Field names should not begin with underscore
        public IniFileLine(string key, string value, string section, bool comment = false)
        {
            this._Key = key;
            this._Value = value;
            this._IsCommented = comment;
            this._Type = IniFileLineType.Directive;
            this._Section = section;
        }

        private string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public IniFileLine(string line, string section)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                this._Type = IniFileLineType.Blank;
                return;
            }

            if (line.TrimStart(" ".ToCharArray()).StartsWith(";"))
            {
                // Marcamos como comentarios para el desparseado y quitamos el caracter.
                this._IsCommented = true;
                line = this.ReplaceFirst(line, ";", string.Empty);
            }

            var parts = Regex.Split(line, "=").ToList();
            if (parts.Count > 1 && ValidKey(parts.First()))
            {
                var rawkey = parts.First();
                parts.RemoveAt(0);
                var rawvalue = string.Join("=", parts);

                this._Key = rawkey.Trim();
                this._Value = rawvalue.Trim();
                this._Type = IniFileLineType.Directive;

                // Creando este template conservamos los espacios originales, si los hubiera.
                this._Template =
                    (string.IsNullOrEmpty(this._Key) ? "{0}" : rawkey.Replace(this._Key, "{0}"))
                    + "="
                    + (string.IsNullOrEmpty(this._Value) ? "{1}" : rawvalue.Replace(this._Value, "{1}"));
            }
            else
            {
                this._Type = IniFileLineType.Comment;
                this._CommentBody = line;
            }
        }
    }
}
