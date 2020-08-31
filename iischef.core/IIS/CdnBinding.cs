using System.Collections.Generic;

namespace iischef.core.IIS
{
    /// <summary>
    /// A CDN binding needs to know about
    /// </summary>
    public class CdnBinding
    {
        /// <summary>
        /// The id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The list of bindings to add to the CDN source. This is where
        /// the edge will connect to in order to pull files
        /// </summary>
        public List<Binding> OriginBindings { get; set; }

        /// <summary>
        /// The list of external domain names used at the edge
        /// </summary>
        public List<string> EdgeUrls { get; set; }
    }
}
