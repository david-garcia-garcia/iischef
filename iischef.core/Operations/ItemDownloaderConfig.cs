using System.Collections.Generic;

namespace iischef.core.Operations
{
    public class ItemDownloaderConfig
    {
        /// <summary>
        /// Uri to download this extension from
        /// </summary>
        public string uri { get; set; }

        /// <summary>
        /// Solo hay dos tipos:
        /// directo
        /// zip
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// File maps to copy from the download
        /// </summary>
        public Dictionary<string, string> maps { get; set; } 
    }
}
