using System.Collections.Generic;

namespace iischef.core.Php
{
    /// <summary>
    /// Represents a PHP extension
    /// </summary>
    public class PhpExtension
    {
        /// <summary>
        /// Extension name.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Uri to download this extension from
        /// </summary>
        public string uri { get; set; }

        /// <summary>
        /// Solo hay dos tipos:
        /// directo -> la URI apunta a un único fichero
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Aux libraries that need to be copied to the PHP runtime folder
        /// </summary>
        public List<string> libraries { get; set; }

        /// <summary>
        /// Get the name of the dll file
        /// </summary>
        /// <returns></returns>
        public string getDllName()
        {
            // TODO: Allow to override this from the config file
            return "php_" + this.name + ".dll";
        }
    }
}
