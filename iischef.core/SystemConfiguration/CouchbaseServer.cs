using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iischef.core.SystemConfiguration
{
    public class CouchbaseServer
    {
        /// <summary>
        /// Server id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Address and port
        /// </summary>
        public string uri { get; set; }

        /// <summary>
        /// Bucket name
        /// </summary>
        public string bucketName { get; set; }

        /// <summary>
        /// Bucket password
        /// </summary>
        public string bucketPassword { get; set; }
    }
}
