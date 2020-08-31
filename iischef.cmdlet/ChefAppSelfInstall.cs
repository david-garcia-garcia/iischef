using iischef.core;
using System.IO;
using System.Management.Automation;

namespace iischef.cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "ChefSelfInstall")]
    public class ChefAppSelfInstall : Cmdlet
    {
        /// <summary>
        /// Drive letter to install chef into...
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string path { get; set; }

        protected override void ProcessRecord()
        {
            ConsoleUtils.RunCode(() =>
            {
                // Try to create the directory.
                var difo = new DirectoryInfo(this.path);
                if (!difo.Exists)
                {
                    Directory.CreateDirectory(this.path);
                }

                var app = ConsoleUtils.GetApplicationForConsole(false);
                app.SelfInstall(this.path, null);
            });
        }
    }
}
