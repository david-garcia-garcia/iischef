using System;
using System.Management.Automation;

namespace iischef.utils
{
    public class PsSession : IDisposable
    {
        private PsSession Session;
        private PowerShell Ps;

        public PsSession(PowerShell ps, string computerName)
        {
            this.Ps = ps;

            if (string.IsNullOrWhiteSpace(computerName))
            {
                return;
            }

            this.Session = ps.AddCommand("New-PSSession")
                .AddParameter("ComputerName", computerName)
                .Invoke<PsSession>()[0];
        }

        public void Dispose()
        {
            // Close the remote session
            this.Ps.AddCommand("Remove-PSSession")
                .AddParameter("Session", this.Session)
                .Invoke();
        }
    }
}
