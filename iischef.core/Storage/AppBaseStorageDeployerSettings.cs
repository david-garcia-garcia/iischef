using System.Collections.Generic;

namespace iischef.core.Storage
{
    public class AppBaseStorageDeployerSettings
    {
        /// <summary>
        /// File paths where configuration settings will be replaced on deploy
        /// </summary>
        public Dictionary<string, string> configuration_replacement_files { get; set; }

        /// <summary>
        /// Paths to dump full settings file intok
        /// Paths to dump full settings file intok
        /// </summary>
        public Dictionary<string, string> configuration_dump_paths { get; set; }

        /// <summary>
        /// Windows privleges to be granted to the user account for this application. Only a small subste of privileges allowed.
        /// </summary>
        public List<string> privileges { get; set; }

        /// <summary>
        /// The account for this application will be added to the provided user groups (that must exist already in the target environment).
        ///
        /// You can use group names, domain qualified names or well-know sid's
        /// </summary>
        public List<string> user_groups { get; set; }

        /// <summary>
        /// Las fuentes a instalar para el aplicativo
        /// </summary>
        public Dictionary<string, AppFont> fonts { get; set; }
    }
}
