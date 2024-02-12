namespace iischef.utils
{
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    public static class DefenderUtils
    {
        public static List<string> GetPathExclusions()
        {
            List<string> results = new List<string>();

            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript("Get-MpPreference | Select-Object -ExpandProperty ExclusionPath");

                    var cmdResults = ps.Invoke();

                    foreach (var item in cmdResults)
                    {
                        results.Add(item.ToString());
                    }
                }

                runspace.Close();
            }

            return results;
        }

        public static void AddPathExclusion(string path)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(string.Format("Add-MpPreference -ExclusionPath {0}", path));

                    ps.Invoke();
                }

                runspace.Close();
            }
        }

        public static void RemovePathExclusion(string path)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(string.Format("Remove-MpPreference -ExclusionPath \"{0}\"", path));

                    ps.Invoke();
                }

                runspace.Close();
            }
        }

        // Method to add a process exclusion
        public static void AddProcessExclusion(string process)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(string.Format("Add-MpPreference -ExclusionProcess \"{0}\"", process));

                    ps.Invoke();
                }

                runspace.Close();
            }
        }

        // Method to remove a process exclusion
        public static void RemoveProcessExclusion(string process)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(string.Format("Remove-MpPreference -ExclusionProcess \"{0}\"", process));

                    ps.Invoke();
                }

                runspace.Close();
            }
        }
    }
}
