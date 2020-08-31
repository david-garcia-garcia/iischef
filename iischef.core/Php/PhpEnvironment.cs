using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace iischef.core.Php
{
    /// <summary>
    /// Represents a PHP environment
    /// </summary>
    public class PhpEnvironment
    {
        /// <summary>
        /// The environment id.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Runtime version. Always assumed NTS and x64.
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// List of deployment operations for the environment
        /// </summary>
        public Dictionary<string, JToken> runtime { get; set; }

        /// <summary>
        /// Environment variables to use for all the PHP runtimes.
        /// </summary>
        public Dictionary<string, string> environmentVariables { get; set; }

        /// <summary>
        /// Allows us to autoprepend files to the environment.
        /// </summary>
        public List<string> autoprependFiles { get; set; }

        /// <summary>
        /// FastCGI process activity timeout.
        /// </summary>
        public int activityTimeout { get; set; }

        /// <summary>
        /// FastCGI request timeout.
        /// </summary>
        public int requestTimeout { get; set; }

        /// <summary>
        /// Max number of PHP processes.
        /// </summary>
        public int maxInstances { get; set; }

        /// <summary>
        /// Maximum number of requests that can be served by a PHP process
        /// before being recycled.
        /// </summary>
        public int instanceMaxRequests { get; set; }
    }
}
