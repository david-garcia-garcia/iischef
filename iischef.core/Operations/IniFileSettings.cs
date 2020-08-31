using iischef.core.Php;
using iischef.utils;

namespace iischef.core.Operations
{
    internal class IniFileSettings
    {
        /// <summary>
        /// Uri to download this extension from
        /// </summary>
        public string key { get; set; }

        /// <summary>
        /// Solo hay dos tipos:
        /// directo
        /// zip
        /// </summary>
        public string value { get; set; }

        /// <summary>
        /// Section name
        /// </summary>
        public string section { get; set; }

        /// <summary>
        /// If this is a multivalue directive
        /// </summary>
        public bool multivalue { get; set; }

        /// <summary>
        /// Wether or not to comment the entry
        /// </summary>
        public bool comment { get; set; }

        /// <summary>
        /// Host based ini settings
        /// </summary>
        public string host { get; set; }

        /// <summary>
        /// For values that point to a directory or file,
        /// ensure the directory is created.
        /// </summary>
        public string ensureDir { get; set; }

        /// <summary>
        /// Execute the opreation...
        /// </summary>
        public void execute(IniFileManager manager, Deployment deployment)
        {
            var val = deployment.ExpandPaths(this.value);

            switch (this.ensureDir)
            {
                case "dir":
                    UtilsSystem.EnsureDirectoryExists(val, true);
                    break;
                case "file":
                    UtilsSystem.EnsureDirectoryExists(val, false);
                    break;
            }

            // If this is a directory or file, make sure we properly quote when
            // writting the PHP.ini, because whitespaces in a path will break
            // most settings
            if (this.ensureDir == "dir" || this.ensureDir == "file")
            {
                if (!val.StartsWith("\""))
                {
                    val = "\"" + val + "\"";
                }
            }

            if (this.multivalue)
            {
                manager.UpdateOrCreateMultivalueDirective(this.key, val, this.section ?? "AUTODEPLOY", this.comment, this.host);
            }
            else
            {
                manager.UpdateOrCreateDirective(this.key, val, this.section ?? "AUTODEPLOY", this.comment, this.host);
            }
        }
    }
}
