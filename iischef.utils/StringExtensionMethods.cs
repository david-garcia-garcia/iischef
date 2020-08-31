// ======================================================================
// | Downloaded From                                                    |
// | Visual C# Kicks - http://www.vcskicks.com/                         |
// | License - http://www.vcskicks.com/license.html                     |
// | Go Pro - http://www.vcskicks.com/components/string-library-pro.php |
// ======================================================================

namespace iischef.utils
{
    public static class StringExtensionMethods
    {
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}
