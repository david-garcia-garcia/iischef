using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace iischef.utils
{
    public static class UtilsEncryption
    {
        public static string GetMD5(string str)
        {
            MD5 md5 = MD5CryptoServiceProvider.Create();
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] stream = null;
            StringBuilder sb = new StringBuilder();
            stream = md5.ComputeHash(encoding.GetBytes(str));
            for (int i = 0; i < stream.Length; i++)
            {
                sb.AppendFormat("{0:x2}", stream[i]);
            }

            return sb.ToString();
        }

        public static string GetSHA1(string str)
        {
            SHA1 sha1 = SHA1Managed.Create();
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] stream = null;
            StringBuilder sb = new StringBuilder();
            stream = sha1.ComputeHash(encoding.GetBytes(str));
            for (int i = 0; i < stream.Length; i++)
            {
                sb.AppendFormat("{0:x2}", stream[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a short hash .
        /// </summary>
        /// <param name="input"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetShortHash(string input, int length = 8)
        {
            var hash = length < 25 ? GetMD5(input) : GetSHA1(input);

            // make sure the hash is only alpha numeric to prevent charecters that may break the url
            return string.Concat(hash.ToCharArray().Where(x => char.IsLetterOrDigit(x)).Take(length));
        }
    }
}
