using System;
using System.Management.Automation;

namespace iischef.core
{
    public class Console : IDisposable
    {
        protected PowerShell ps;

        public Console()
        {
            this.ps = PowerShell.Create();
        }

        /// <summary>
        /// Runs a script synchronously
        /// </summary>
        /// <param name="command"></param>
        public void RunCommand(string command)
        {
            this.ps.AddScript(command);
            this.ps.Invoke();

            if (this.ps.Streams.Error.Count > 0)
            {
                throw new Exception("Error while running console commands.");
            }
        }

        public void Dispose()
        {
            if (this.ps != null)
            {
                this.ps.Dispose();
                this.ps = null;
            }
        }
    }
}
