using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;

namespace iischef.utils
{
    /// <summary>
    /// Custom parser for identity identifiers
    /// </summary>
    public class FqdnNameParser
    {
        /// <summary>
        /// Account name
        /// </summary>
        public string SamAccountName { get; set; }

        /// <summary>
        /// User principal name
        /// </summary>
        public string UserPrincipalName { get; set; }

        /// <summary>
        /// Security identifier if any
        /// </summary>
        public SecurityIdentifier Sid { get; set; }

        /// <summary>
        /// The domain name
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// The context type, if this could be inferred from the provided identifier
        /// </summary>
        public ContextType? ContextType { get; set; }

        /// <summary>
        /// Get an instance of FqdnNameParser
        /// </summary>
        /// <param name="name"></param>
        /// <param name="autoCalculateSamAccountName">Usernames used by CHEF al have an autocalculated sam account name</param>
        public FqdnNameParser(string name, bool autoCalculateSamAccountName = true)
        {
            // Backwards compatibility fix
            if (name.StartsWith("sid:"))
            {
                name = name.Replace("sid:", string.Empty).Trim();
            }

            if (name.Contains("@"))
            {
                this.DomainName = name.Split("@".ToCharArray()).Last();
                name = name.Split("@".ToCharArray()).First();
            }

            if (name.Contains("\\"))
            {
                this.DomainName = name.Split("\\".ToCharArray()).First();
                name = name.Split("\\".ToCharArray()).Last();
            }

            if (UtilsSystem.IsValidSid(name))
            {
                // Assume we are using the user principal name, we cannot determine the context here...
                this.Sid = new SecurityIdentifier(name);
            }
            else
            {
                this.UserPrincipalName = name;

                if (autoCalculateSamAccountName)
                {
                    this.SamAccountName = UtilsWindowsAccounts.SamAccountNameFromUserPrincipalName(this.UserPrincipalName);
                }
            }

            if (name.StartsWith("#"))
            {
                throw new Exception("Unsupported prefix # used in identity definition.");

                // Assume we are using the user principal name, we cannot determine the context here...
                // this.UserPrincipalName = name.Replace("#", string.Empty);
                // this.SamAccountName = name.Replace("#", string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(this.DomainName))
            {
                this.ContextType = (Environment.MachineName == this.DomainName || this.DomainName.Equals("localhost", StringComparison.CurrentCultureIgnoreCase))
                    ? System.DirectoryServices.AccountManagement.ContextType.Machine
                    : System.DirectoryServices.AccountManagement.ContextType.Domain;
            }
        }
    }
}
