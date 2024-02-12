using System.Net;

namespace iischef.utils
{
    public static class ServicePointManagerExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static void SetupServicePointManager()
        {
            ServicePointManager.Expect100Continue = true;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                                                   | SecurityProtocolType.Tls11
                                                   | SecurityProtocolType.Tls12
                                                   | SecurityProtocolType.Ssl3;
        }
    }
}
