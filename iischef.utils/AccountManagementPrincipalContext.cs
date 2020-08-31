using System.DirectoryServices.ActiveDirectory;
using System.Linq;

namespace iischef.utils
{
    /// <summary>
    /// The information used to handle user creation and permission management for applications
    /// </summary>
    public class AccountManagementPrincipalContext
    {
        public string ContextType { get; set; }

        public string ContextName { get; set; }

        public string Container { get; set; }

        public string ContextOptions { get; set; }

        public string ContextUsername { get; set; }

        public string ContextPassword { get; set; }

        /// <summary>
        /// Optional domain name, will be automatically resolved to the current machine's domain name if empty
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userPrincipalName"></param>
        /// <param name="preWindows2000"></param>
        /// <returns></returns>
        public string FormatUserNameForPrincipal(string userPrincipalName, bool preWindows2000)
        {
            // If this is a domain principal, use a domain name.
            if (this.ContextType == nameof(System.DirectoryServices.AccountManagement.ContextType.Domain))
            {
                string domainName = this.DomainName;

                if (string.IsNullOrWhiteSpace(domainName))
                {
                    domainName = Domain.GetComputerDomain().Name;

                    // For compatiblity with SQL Server and legacy apps, use main DC.
                    // As sometimes domain name will return DC1.DC2.DC3
                    // and we only need DC1
                    domainName = domainName.Split(".".ToCharArray()).First().ToUpper();
                }

                if (preWindows2000)
                {
                    string mainDomain = domainName.Split(".".ToCharArray()).First().ToUpper();
                    string samAccountName = UtilsWindowsAccounts.SamAccountNameFromUserPrincipalName(userPrincipalName);
                    return $"{mainDomain}\\{samAccountName}";
                }
                else
                {
                    return $"{userPrincipalName}@{domainName}";
                }
            }

            return UtilsWindowsAccounts.SamAccountNameFromUserPrincipalName(userPrincipalName);
        }
    }
}
