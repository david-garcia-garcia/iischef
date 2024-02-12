using System;
using System.Linq;
using System.Text.RegularExpressions;

public static class HostnameValidator
{
    public static bool IsValidHostname(string hostname)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            return false;
        }

        if (hostname.Length > 253)
        {
            return false;
        }

        var labelRegex = new Regex(@"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)$");
        return hostname.Split('.').All(label => labelRegex.IsMatch(label));
    }
}