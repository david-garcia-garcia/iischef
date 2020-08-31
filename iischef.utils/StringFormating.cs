using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace iischef.utils
{
    public class StringFormating : SingletonedClass<StringFormating>
    {
        private System.Text.Encoding Base64StringEncodeDecodeDefaultEncoding
        {
            get
            {
                return System.Text.Encoding.UTF8;
            }
        }

        public static Encoding GetFileEncodingBOM(byte[] buffer)
        {
            // *** Use Default of Encoding.Default (Ansi CodePage)
            Encoding enc = Encoding.Default;

            var len = buffer.Count();

            if (len > 3 && buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
            {
                enc = Encoding.UTF8;
            }
            else if (len > 2 && buffer[0] == 0xfe && buffer[1] == 0xff)
            {
                enc = Encoding.Unicode;
            }
            else if (len > 4 && buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
            {
                enc = Encoding.UTF32;
            }
            else if (len > 3 && buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
            {
                enc = Encoding.UTF7;
            }
            else if (len > 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                // 1201 unicodeFFFE Unicode (Big-Endian)
                enc = Encoding.GetEncoding(1201);
            }
            else if (len > 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                // 1200 utf-16 Unicode
                enc = Encoding.GetEncoding(1200);
            }

            // No hemos sido capaces de detectar la codificación
            if (enc == Encoding.Default)
            {
                return null;
            }

            return enc;
        }

        public string Base64Encode(string data, System.Text.Encoding enc = null)
        {
            if (enc == null)
            {
                enc = this.Base64StringEncodeDecodeDefaultEncoding;
            }

            byte[] toEncodeAsBytes = enc.GetBytes(data);
            string returnValue = System.Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }

        public string Base64Decode(string encodedData, System.Text.Encoding enc = null)
        {
            if (enc == null)
            {
                enc = this.Base64StringEncodeDecodeDefaultEncoding;
            }

            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);
            string returnValue = enc.GetString(encodedDataAsBytes);
            return returnValue;
        }

        public string PrepareStringForUrlFileName(string name)
        {
            name = this.RemoveDiacritics(name, true).ToLower();

            // INTENTAMOS QUITAR PALÁBRAS CON LONGITUD MENOR A 2 CARÁCTERES
            var words = name.Split(" ".ToCharArray());

            var newName = string.Empty;

            foreach (var w in words)
            {
                if (w.Length > 2)
                {
                    if (newName.Length > 0)
                    {
                        newName += "_";
                    }

                    newName += this.ExtremeClean(w).Trim();
                }
            }

            if (newName.Length == 0
                || newName.Length < name.Length * 0.25)
            {
                newName = name.Replace(" ", "_");
            }

            return newName;
        }

        public IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm, Func<char, char> customFolding)
        {
            foreach (char c in src.Normalize(compatNorm ? NormalizationForm.FormKD : NormalizationForm.FormD))
            {
                switch (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c))
                {
                    case System.Globalization.UnicodeCategory.NonSpacingMark:
                    case System.Globalization.UnicodeCategory.SpacingCombiningMark:
                    case System.Globalization.UnicodeCategory.EnclosingMark:

                        // do nothing
                        break;
                    default:
                        yield return customFolding(c);
                        break;
                }
            }
        }

        public IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm)
        {
            if (src == null)
            {
                return null;
            }

            return this.RemoveDiacritics(src, compatNorm, c => c);
        }

        public string RemoveDiacritics(string src, bool compatNorm, Func<char, char> customFolding)
        {
            if (src == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            foreach (char c in this.RemoveDiacriticsEnum(src, compatNorm, customFolding))
            {
                sb.Append(c);
            }

            return sb.ToString();
        }

        public string RemoveDiacritics(string src, bool compatNorm)
        {
            if (src == null)
            {
                return null;
            }

            return this.RemoveDiacritics(src, compatNorm, c => c);
        }

        public string RemoveInvalidXmlChars(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            var isValid = new Predicate<char>(value =>
                (value >= 0x0020 && value <= 0xD7FF) ||
                (value >= 0xE000 && value <= 0xFFFD) ||
                value == 0x0009 ||
                value == 0x000A ||
                value == 0x000D);

            return new string(Array.FindAll(input.ToCharArray(), isValid));
        }

        /// <summary>
        /// Devuelve un string de la longitud solicitada agregando el caracter "0" al inicio del string
        /// hasta un máximo de 50 posiciones
        /// </summary>
        /// <param name="str"></param>
        public string ToFixedLengthIntegerString(int number, int length)
        {
            string res = number.ToString();

            int diferencia = length - res.Length;

            if (diferencia > 0)
            {
                string plantilla = string.Empty;
                for (int x = 0; x < length; x++)
                {
                    plantilla += "0";
                }

                return plantilla.Substring(0, diferencia) + res;
            }
            else
            {
                return res;
            }
        }

        public List<string> split(string input, string delimitador)
        {
            if (!input.Contains(delimitador))
            {
                return new List<string>() { delimitador };
            }

            List<string> resultados = new List<string>();
            while (input.Contains(delimitador))
            {
                int position = input.IndexOf(delimitador);
                string content = input.Substring(0, position + delimitador.Length);
                input = input.Substring(position + delimitador.Length, input.Length - (position + delimitador.Length));
                resultados.Add(content.Replace(delimitador, string.Empty));
            }

            return resultados;
        }

        public string LeaveOnlyDigits(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "[^0-9]", string.Empty);
        }

        /// <summary>
        /// DEJA EL STRING SOLO CON LETRAS (MAYÚSCULAS Y MINÚSCULAS) y NÚMEROS
        /// </summary>
        /// <param name="url"></param>
        /// <param name="aditionalChars"></param>
        /// <returns></returns>
        public string ExtremeClean(string url, List<string> aditionalChars = null)
        {
            if (url == null)
            {
                return null;
            }

            // INTENTAMOS QUITAR ACENTOS PARA QUE SE PIERDA EL MINIMO DE INFORMACIÓN
            try
            {
                url = this.RemoveDiacritics(url, true);
            }
            catch 
            { 
            }

            List<string> posibles = new List<string>() 
            { 
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t",
            "u", "v", "w", "x", "y", "z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"
            };

            if (aditionalChars != null)
            {
                posibles.AddRange(aditionalChars);
            }

            string result = string.Empty;

            foreach (char c in url)
            {
                if (posibles.Contains(c.ToString()))
                {
                    result += c.ToString();
                }
            }

            return result;
        }

        public string ExtremeClean2(string url)
        {
            if (url == null)
            {
                return null;
            }

            List<string> posibles = new List<string>() 
            { 
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t",
            "u", "v", "w", "x", "y", "z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "_", "-"
            };

            string result = string.Empty;

            foreach (char c in url)
            {
                if (posibles.Contains(c.ToString()))
                {
                    result += c.ToString();
                }
            }

            return result;
        }

        public string RemoveNumbers(string url)
        {
            List<string> posibles = new List<string>() 
            { 
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
            };

            string result = string.Empty;

            foreach (char c in url)
            {
                if (!posibles.Contains(c.ToString()))
                {
                    result += c.ToString();
                }
            }

            return result;
        }

        public string RemoveTextBetweenChars(string content, string initChar, string endChar)
        {
            int pos1 = -1; 
            int pos2 = -1;

            pos1 = content.IndexOf(initChar);
            if (pos1 != -1)
            {
                pos2 = content.IndexOf(endChar, pos1 + 1);
            }

            while (pos2 != -1 && pos1 != -1)
            {
                content = content.Substring(0, pos1)
                    + content.Substring(pos2 + initChar.Length, content.Length - pos2 - initChar.Length);
                pos1 = content.IndexOf(initChar);
                if (pos1 != -1)
                {
                    pos2 = content.IndexOf(endChar, pos1 + 1);
                }
            }

            return content;
        }
    }
}
