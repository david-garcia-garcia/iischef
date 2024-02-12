using iischef.logger;
using iischef.utils;
using System;
using System.Management.Automation;
using System.Runtime.InteropServices;

/// <summary>
/// Clase alternative de utilidades de manipulación de cuentas porque en los contenedores
/// no se puede utilizar nada del namespace System.DirectoryServices
/// </summary>
public class UtilsAccountManagement
{
    /// <summary>
    /// Creates or updates a user with the given username and password.
    /// </summary>
    /// <param name="user">Username</param>
    /// <param name="password">Password</param>
    public static void UpsertUser(string user, string password, ILoggerInterface logger)
    {
        using (PowerShell ps = PowerShell.Create())
        {
            string script = $"$user = Get-LocalUser | Where-Object {{ $_.Name -eq '{user}' }};" +
                            $"if ($user) {{ " +
                            $"Set-LocalUser -Name '{user}' -Password (ConvertTo-SecureString '{password}' -AsPlainText -Force) -PasswordNeverExpires $true;" +
                            $"}} else {{ " +
                            $"New-LocalUser -Name '{user}' -Password (ConvertTo-SecureString '{password}' -AsPlainText -Force) -AccountNeverExpires -UserMayNotChangePassword -PasswordNeverExpires;" +
                            $"}}";

            ps.AddScript(script);
            ps.InvokeAndTreatError(logger);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <param name="logger"></param>
    public static void DeleteUser(string user, ILoggerInterface logger)
    {
        using (PowerShell ps = PowerShell.Create())
        {
            string script = $"$user = Get-LocalUser | Where-Object {{ $_.Name -eq '{user}' }};" +
                            $"if ($user) {{ " +
                            $"Remove-LocalUser -Name '{user}' " +
                            $"}} else {{ " +
                            $"Write-Host 'User {user} does not exist.' " +
                            $"}}";

            ps.AddScript(script);
            ps.InvokeAndTreatError(logger); // Assuming InvokeAndTreatError is a method for error handling
        }
    }

    /// <summary>
    /// Ensures a user is a member of a specified group.
    /// </summary>
    /// <param name="user">User FQDN or SID</param>
    /// <param name="group">Group FQDN or SID</param>
    public static void EnsureUserInGroup(string user, string group, ILoggerInterface logger)
    {
        group = ResolveGroupName(group, logger);

        if (IsUserInGroup(user, group, logger))
        {
            return;
        }

        using (PowerShell ps = PowerShell.Create())
        {
            string script = $"Add-LocalGroupMember -Group '{group}' -Member '{user}'";
            ps.AddScript(script);
            ps.InvokeAndTreatError(logger);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    public static bool IsUserInGroup(string user, string group, ILoggerInterface logger)
    {
        using (PowerShell ps = PowerShell.Create())
        {
            // Prefix the user with the machine name
            user = $"{Environment.MachineName}\\{user}";

            string script = $"$group = Get-LocalGroup -Name '{group}' -ErrorAction SilentlyContinue; " +
                            $"if ($group) {{ " +
                            $"  $members = Get-LocalGroupMember -Group '{group}' -ErrorAction SilentlyContinue; " +
                            $"  $isMember = $false;" +
                            $"  foreach ($member in $members) {{ " +
                            $"    if ($member.Name -eq '{user}') {{ " +
                            $"      $isMember = $true; " +
                            $"      break; " +
                            $"    }} " +
                            $"  }} " +
                            $"  $isMember;" +
                            $"}} else {{ " +
                            $"  $false " +
                            $"}}";

            ps.AddScript(script);
            var results = ps.InvokeAndTreatError(logger);
            if (ps.HadErrors || results.Count == 0)
            {
                return false;
            }

            return (bool)results[0].BaseObject;
        }
    }

    /// <summary>
    /// Ensures a user is not a member of a specified group.
    /// </summary>
    /// <param name="user">User FQDN or SID</param>
    /// <param name="group">Group FQDN or SID</param>
    /// <param name="logger"></param>
    public static void EnsureUserNotInGroup(string user, string group, ILoggerInterface logger)
    {
        group = ResolveGroupName(group, logger);

        if (!IsUserInGroup(user, group, logger))
        {
            return;
        }

        using (PowerShell ps = PowerShell.Create())
        {
            string script = $"Remove-LocalGroupMember -Group '{group}' -Member '{user}'";
            ps.AddScript(script);
            ps.InvokeAndTreatError(logger);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool UserExists(string name, ILoggerInterface logger)
    {
        using (PowerShell ps = PowerShell.Create())
        {
            ps.AddScript(
                $"$user = Get-LocalUser | Where-Object {{ $_.Name -eq '{name}' }}; if ($user) {{ $true }} else {{ $false }}");
            var result = ps.InvokeAndTreatError(logger);
            return result.Count > 0 && (bool)result[0].BaseObject;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="groupIdentifier"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static string ResolveGroupName(string groupIdentifier, ILoggerInterface logger)
    {
        using (PowerShell ps = PowerShell.Create())
        {
            string script = $"if ('{groupIdentifier}' -match 'S-1-') {{ " +
                            $"  $group = Get-LocalGroup | Where-Object {{ $_.SID.Value -eq '{groupIdentifier}' }}; " +
                            $"  if ($group) {{ $group.Name }} else {{ $null }}" +
                            $"}} else {{ " +
                            $"  $group = Get-LocalGroup -Name '{groupIdentifier}' -ErrorAction SilentlyContinue; " +
                            $"  if ($group) {{ $group.Name }} else {{ $null }}" +
                            $"}}";

            ps.AddScript(script);

            var results = ps.InvokeAndTreatError(logger);

            if (ps.HadErrors || results.Count == 0)
            {
                return null; // Group not found or an error occurred
            }

            return results[0].ToString();
        }
    }

    // Import the LogonUser function from the Windows API
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(string username, string domain, string password, int logonType, int logonProvider, out IntPtr token);

    /// <summary>
    /// Validates user credentials.
    /// </summary>
    /// <param name="userName">Username</param>
    /// <param name="password">Password</param>
    /// <param name="logger">Logger interface</param>
    /// <returns>True if credentials are valid, False otherwise</returns>
    public static bool CheckUserAndPassword(string userName, string password, ILoggerInterface logger)
    {
        IntPtr token = IntPtr.Zero;

        try
        {
            var domainUser = new DomainUserParser(userName);

            // Attempt to log the user on
            bool result = LogonUser(domainUser.Username, domainUser.Domain, password, 2, 0, out token);

            // Close the token handle
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error validating credentials: {ex.Message}");
            return false;
        }
    }

    // Import the CloseHandle function from the Windows API
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}