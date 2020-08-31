using iischef.logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace iischef.utils
{
    /// <summary>
    /// Utils to handle permission management
    /// </summary>
    public static class UtilsWindowsAccounts
    {
        // https://support.microsoft.com/en-us/help/243330/well-known-security-identifiers-in-windows-operating-systems

        public const string WELL_KNOWN_SID_EVERYONE = "S-1-1-0";

        public const string WELL_KNOWN_SID_USERS = "S-1-5-32-545";

        public const string WELL_KNOWN_SID_IIS_USERS = "S-1-5-32-568";

        public const string WELL_KNOWN_SID_AUTHENTICATED_USERS = "S-1-5-11";

        /// <summary>
        /// Generate a valid directory principal, removing any OU specification
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static PrincipalContext BuildPrincipalWithoutOu(AccountManagementPrincipalContext context)
        {
            if (context == null)
            {
                return BuildPrincipal(null);
            }

            var ctx = context.JsonClone();

            var container = new ContainerParser(ctx.Container);
            for (int x = 0; x < container.Parts.Count; x++)
            {
                if (container.Parts[x].Item1.Equals("OU", StringComparison.CurrentCultureIgnoreCase))
                {
                    container.Parts.RemoveAt(x);
                    x--;
                }
            }

            ctx.Container = container.GetContainer();

            return BuildPrincipal(ctx);
        }

        /// <summary>
        /// Generate a valid directory principal based on the application settings.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static PrincipalContext BuildPrincipal(AccountManagementPrincipalContext context)
        {
            if (context == null)
            {
                // Default to old configuration...
                return new PrincipalContext(ContextType.Machine, Environment.MachineName);
            }

            ContextType contextType;

            switch (context.ContextType)
            {
                case nameof(ContextType.Machine):
                    contextType = ContextType.Machine;
                    break;
                case nameof(ContextType.ApplicationDirectory):
                    contextType = ContextType.ApplicationDirectory;
                    break;
                case nameof(ContextType.Domain):
                    contextType = ContextType.Domain;
                    break;
                default:
                    throw new Exception($"Context type not supported: " + context.ContextType);
            }

            ContextOptions? options = null;

            switch (context.ContextOptions)
            {
                case nameof(ContextOptions.Negotiate):
                    options = ContextOptions.Negotiate;
                    break;
                case nameof(ContextOptions.Sealing):
                    options = ContextOptions.Sealing;
                    break;
                case nameof(ContextOptions.SecureSocketLayer):
                    options = ContextOptions.SecureSocketLayer;
                    break;
                case nameof(ContextOptions.Signing):
                    options = ContextOptions.Signing;
                    break;
                case nameof(ContextOptions.ServerBind):
                    options = ContextOptions.ServerBind;
                    break;
                case nameof(ContextOptions.SimpleBind):
                    options = ContextOptions.SimpleBind;
                    break;
                case null:
                    break;
                default:
                    throw new Exception($"Context option not supported: " + context.ContextOptions);
            }

            if (options != null)
            {
                return new PrincipalContext(contextType, context.ContextName, context.Container, options.Value, context.ContextUsername, context.ContextPassword);
            }
            else
            {
                return new PrincipalContext(contextType, context.ContextName, context.Container, context.ContextUsername, context.ContextPassword);
            }
        }

        public static void ValidatePrivilege(string name)
        {
            List<string> validPrivileges = new List<string>();

            validPrivileges.Add("SeCreateSymbolicLinkPrivilege");
            validPrivileges.Add("SeDebugPrivilege");
            validPrivileges.Add("SeBatchLogonRight");

            if (!validPrivileges.Contains(name))
            {
                throw new Exception($"Invalid privilege name: {name}");
            }
        }

        /// <summary>Adds a privilege to an account. Only works for local accounts.</summary>
        /// <param name="accountName">Name of an account - "domain\account" or only "account"</param>
        /// <param name="privilegeName">Name of the privilege (ms-help://MS.VSCC/MS.MSDNVS.1031/security/accctrl_96lv.htm")</param>
        /// <param name="logger"></param>
        /// <returns>The windows error code returned by LsaAddAccountRights</returns>
        public static long SetRight(string accountName, string privilegeName, ILoggerInterface logger)
        {
            ValidatePrivilege(privilegeName);

            logger.LogInfo(true, $"SetRight '{privilegeName}' for account '{accountName}'");

            long winErrorCode = 0; // contains the last error
            var rights = new List<string>();

            // pointer an size for the SID
            IntPtr sid = IntPtr.Zero;
            int sidSize = 0;

            // StringBuilder and size for the domain name
            StringBuilder domainName = new StringBuilder();
            int nameSize = 0;

            // account-type variable for lookup
            int accountType = 0;

            // get required buffer size
            Advapi32Extern.LookupAccountName(string.Empty, accountName, sid, ref sidSize, domainName, ref nameSize, ref accountType);

            // allocate buffers
            domainName = new StringBuilder(nameSize);
            sid = Marshal.AllocHGlobal(sidSize);

            // lookup the SID for the account
            bool result = Advapi32Extern.LookupAccountName(string.Empty, accountName, sid, ref sidSize, domainName, ref nameSize, ref accountType);

            // say what you're doing
            logger.LogInfo(true, "LookupAccountName result = " + result);
            logger.LogInfo(true, $"IsValidSid: {Advapi32Extern.IsValidSid(sid)}");
            logger.LogInfo(true, $"LookupAccountName succedded: [domainName='{domainName}']");

            if (!result)
            {
                winErrorCode = Advapi32Extern.GetLastError();
                logger.LogInfo(false, "LookupAccountName failed: " + winErrorCode);
                return winErrorCode;
            }

            // initialize an empty unicode-string
            Advapi32Extern.LSA_UNICODE_STRING systemName = new Advapi32Extern.LSA_UNICODE_STRING();

            // initialize a pointer for the policy handle
            IntPtr policyHandle = IntPtr.Zero;

            // these attributes are not used, but LsaOpenPolicy wants them to exists
            Advapi32Extern.LSA_OBJECT_ATTRIBUTES objectAttributes = new Advapi32Extern.LSA_OBJECT_ATTRIBUTES();
            objectAttributes.Length = 0;
            objectAttributes.RootDirectory = IntPtr.Zero;
            objectAttributes.Attributes = 0;
            objectAttributes.SecurityDescriptor = IntPtr.Zero;
            objectAttributes.SecurityQualityOfService = IntPtr.Zero;

            // get a policy handle
            logger.LogInfo(true, "OpenPolicy started");
            int resultPolicy = Advapi32Extern.LsaOpenPolicy(ref systemName, ref objectAttributes, Advapi32Extern.Access, out policyHandle);
            winErrorCode = Advapi32Extern.LsaNtStatusToWinError(resultPolicy);

            if (winErrorCode != 0)
            {
                logger.LogInfo(false, "OpenPolicy failed: " + winErrorCode);
                return winErrorCode;
            }

            IntPtr userRightsPtr = IntPtr.Zero;
            int countOfRights = 0;

            logger.LogInfo(true, "LsaEnumerateAccountRights started");
            int resultEnumerate = Advapi32Extern.LsaEnumerateAccountRights(policyHandle, sid, out userRightsPtr, out countOfRights);
            winErrorCode = Advapi32Extern.LsaNtStatusToWinError(resultEnumerate);
            if (winErrorCode != 0 && winErrorCode != 2)
            {
                logger.LogInfo(false, "LsaEnumerateAccountRights failed: " + winErrorCode);
                return winErrorCode;
            }

            // Code 2 means no privileges
            if (winErrorCode == 0)
            {
                long ptr = userRightsPtr.ToInt64();
                Advapi32Extern.LSA_UNICODE_STRING userRight;

                for (int i = 0; i < countOfRights; i++)
                {
                    userRight = (Advapi32Extern.LSA_UNICODE_STRING)Marshal.PtrToStructure(userRightsPtr, typeof(Advapi32Extern.LSA_UNICODE_STRING));
                    string userRightStr = Marshal.PtrToStringAuto(userRight.Buffer);
                    rights.Add(userRightStr);
                    ptr += Marshal.SizeOf(userRight);
                }

                if (rights.Contains(privilegeName))
                {
                    logger.LogInfo(false, $"Account already has right '{privilegeName}'");
                    return winErrorCode;
                }
            }

            // Now that we have the SID an the policy,
            // we can add rights to the account.

            // initialize an unicode-string for the privilege name
            Advapi32Extern.LSA_UNICODE_STRING[] userRights = new Advapi32Extern.LSA_UNICODE_STRING[1];
            userRights[0] = new Advapi32Extern.LSA_UNICODE_STRING();
            userRights[0].Buffer = Marshal.StringToHGlobalUni(privilegeName);
            userRights[0].Length = (ushort)(privilegeName.Length * UnicodeEncoding.CharSize);
            userRights[0].MaximumLength = (ushort)((privilegeName.Length + 1) * UnicodeEncoding.CharSize);

            // add the right to the account
            logger.LogInfo(true, "LsaAddAccountRights started");
            int res = Advapi32Extern.LsaAddAccountRights(policyHandle, sid, userRights, 1);
            winErrorCode = Advapi32Extern.LsaNtStatusToWinError(res);

            if (winErrorCode != 0)
            {
                logger.LogInfo(false, "LsaAddAccountRights failed: " + winErrorCode);
                return winErrorCode;
            }

            rights.Add(privilegeName);

            Advapi32Extern.LsaClose(policyHandle);

            Advapi32Extern.FreeSid(sid);

            return winErrorCode;
        }

        /// <summary>
        /// Add this permission to a directory and user
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="directory"></param>
        /// <param name="fileSystemRights"></param>
        /// <param name="acp"></param>
        public static void AddPermissionToDirectoryIfMissing(
            string identity,
            string directory,
            FileSystemRights fileSystemRights,
            AccountManagementPrincipalContext acp)
        {
            // Search for the target principal in all the domain
            using (PrincipalContext context = BuildPrincipalWithoutOu(acp))
            {
                var principal = FindIdentity(identity, context);

                if (principal == null)
                {
                    throw new Exception($"Unable to find principal with identifier: {identity}");
                }

                AddPermissionToDirectoryIfMissing(principal.Sid, directory, fileSystemRights);
            }
        }

        /// <summary>
        /// Add the Everyone permission to a directory
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ssid"></param>
        /// <param name="permissions"></param>
        public static void AddEveryonePermissionToDir(string path, string ssid, FileSystemRights permissions = FileSystemRights.Read)
        {
            var directoryInfo = new DirectoryInfo(path);

            DirectorySecurity dSecurity = Directory.GetAccessControl(path);

            var identity = new SecurityIdentifier(ssid);

            FileSystemAccessRule accessRule = new FileSystemAccessRule(
                identity,
                fileSystemRights: permissions,
                inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                propagationFlags: PropagationFlags.None,
                type: AccessControlType.Allow);

            // Add the FileSystemAccessRule to the security settings. 
            dSecurity.AddAccessRule(accessRule);

            // Set the new access settings.
            directoryInfo.SetAccessControl(dSecurity);
        }

        /// <summary>
        /// To ease management, we need a one-to-one transformation from userPrincipalName to SamAccountName
        /// </summary>
        /// <param name="userPrincipalName">User principal name, must contain a prefix: prefix_accoutnname</param>
        /// <returns></returns>
        public static string SamAccountNameFromUserPrincipalName(string userPrincipalName)
        {
            if (!userPrincipalName.Contains("_"))
            {
                throw new Exception($"User name '{userPrincipalName}' does not start with a prefix and an underscore: prefix_username");
            }

            string samAccountName = userPrincipalName;
            string prefix = userPrincipalName.Split("_".ToCharArray()).First() + "_";

            if (prefix.Length > 5)
            {
                throw new Exception($"The length of the prefix '{prefix}' exceeds the 4 character limit.");
            }

            string cleanName = string.Join("_", userPrincipalName.Split("_".ToCharArray()).Skip(1));

            // The maximum user length depends on if this is a local account, or a domain account.
            // https://serverfault.com/questions/105142/windows-server-2008-r2-change-the-maximum-username-length
            int maxUserNameLength = 20;

            if (samAccountName.Length > maxUserNameLength)
            {
                samAccountName = prefix + UtilsEncryption
                                                 .GetMD5(cleanName)
                                                 .Substring(0, maxUserNameLength - prefix.Length);
            }

            return samAccountName;
        }

        /// <summary>
        /// Disable permission inheritance in a directory
        /// </summary>
        /// <param name="path"></param>
        /// <param name="preserveInheritance"></param>
        public static void DisablePermissionInheritance(string path, bool preserveInheritance = true)
        {
            DirectorySecurity directorySecurity = Directory.GetAccessControl(path);
            directorySecurity.SetAccessRuleProtection(true, preserveInheritance);
            Directory.SetAccessControl(path, directorySecurity);
        }

        /// <summary>
        /// Add permissions to a directory if missing
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="directory"></param>
        /// <param name="fileSystemRights"></param>
        public static void AddPermissionToDirectoryIfMissing(
            IdentityReference identity,
            string directory,
            FileSystemRights fileSystemRights)
        {
            var directoryInfo = new DirectoryInfo(directory);

            // Get a DirectorySecurity object that represents the current security settings.
            DirectorySecurity dSecurity = directoryInfo.GetAccessControl();

            var rules = dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
            FileSystemAccessRule currentRule = null;
            foreach (AuthorizationRule r in rules)
            {
                if (r.IdentityReference == identity)
                {
                    currentRule = (FileSystemAccessRule)r;
                    break;
                }
            }

            FileSystemAccessRule accessRule = new FileSystemAccessRule(
                             identity,
                             fileSystemRights: fileSystemRights,
                             inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                             propagationFlags: PropagationFlags.None,
                             type: AccessControlType.Allow);

            // If there is an existing rule that completely matches our configuration,
            // do nothing as SetAccessControl() calls can be extremely slow
            // depending on circumstances.
            if (currentRule != null
                && currentRule.FileSystemRights.HasFlag(accessRule.FileSystemRights)
                && currentRule.InheritanceFlags.HasFlag(accessRule.InheritanceFlags)
                && currentRule.PropagationFlags.HasFlag(accessRule.PropagationFlags)
                && currentRule.AccessControlType.HasFlag(accessRule.AccessControlType))
            {
                return;
            }

            // Add the FileSystemAccessRule to the security settings. 
            dSecurity.AddAccessRule(accessRule);

            // Set the new access settings.
            directoryInfo.SetAccessControl(dSecurity);
        }

        /// <summary>
        /// Add permissions to a directory if missing
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="directory"></param>
        /// <param name="logger"></param>
        public static void RemoveAccessRulesForIdentity(
            IdentityReference identity,
            string directory,
            ILoggerInterface logger)
        {
            var directoryInfo = new DirectoryInfo(directory);

            // Get a DirectorySecurity object that represents the current security settings.
            DirectorySecurity dSecurity = directoryInfo.GetAccessControl();

            bool removed = false;

            var rules = dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
            foreach (AuthorizationRule r in rules)
            {
                if (r.IdentityReference == identity)
                {
                    var currentRule = (FileSystemAccessRule)r;
                    dSecurity.RemoveAccessRule(currentRule);
                    removed = true;
                }
            }

            if (removed)
            {
                directoryInfo.SetAccessControl(dSecurity);
            }
            else
            {
                logger.LogInfo(true, "Could not find any rule to remove for identity {0}", identity.Value);
            }
        }

        /// <summary>
        /// Delete a user
        /// </summary>
        /// <param name="username"></param>
        /// <param name="acp"></param>
        public static void DeleteUser(
            string username,
            AccountManagementPrincipalContext acp)
        {
            using (PrincipalContext context = BuildPrincipal(acp))
            {
                var user = FindUser(username, context);
                user?.Delete();
            }
        }

        /// <summary>
        /// Add a user to a group, 
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="acp"></param>
        /// <returns></returns>
        public static void EnsureGroupExists(
            string groupName,
            AccountManagementPrincipalContext acp)
        {
            using (PrincipalContext context = BuildPrincipal(acp))
            {
                GroupPrincipal g = FindGroup(groupName, context);

                if (g == null)
                {
                    g = new GroupPrincipal(context);
                    g.Name = groupName;
                    g.SamAccountName = groupName;
                    g.Description = groupName;

                    if (context.ContextType == ContextType.Domain)
                    {
                        g.UserPrincipalName = groupName;
                    }
                }

                g.Save();
            }
        }

        /// <summary>
        /// Chef if user is in a group.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="groupName"></param>
        /// <param name="acp"></param>
        /// <returns></returns>
        public static bool IsUserInGroup(
            string userName,
            string groupName,
            AccountManagementPrincipalContext acp)
        {
            using (var context = BuildPrincipal(acp))
            {
                using (var searcher = new PrincipalSearcher(new UserPrincipal(context) { SamAccountName = userName }))
                {
                    using (var user = searcher.FindOne() as UserPrincipal)
                    {
                        return user != null && user.IsMemberOf(context, IdentityType.SamAccountName, groupName);
                    }
                }
            }
        }

        /// <summary>
        /// Ensure user is in group
        /// </summary>
        /// <param name="userPrincipalName"></param>
        /// <param name="groupname"></param>
        /// <param name="logger"></param>
        /// <param name="acp"></param>
        public static void EnsureUserInGroup(
            string userPrincipalName,
            string groupname,
            ILoggerInterface logger,
            AccountManagementPrincipalContext acp)
        {
            logger.LogInfo(true, $"Ensure user '{userPrincipalName}' in group '{groupname}'");

            UserPrincipal user = SearchUser(userPrincipalName, acp, out var userContext);

            if (user == null)
            {
                userContext?.Dispose();
                throw new Exception($"User '{userPrincipalName}' not found.");
            }

            GroupPrincipal group = SearchGroup(groupname, acp, out var groupContext);

            if (group == null)
            {
                userContext?.Dispose();
                groupContext?.Dispose();
                throw new Exception($"Group '{groupname}' not found.");
            }

            logger.LogWarning(false, $"Found group '{group.Name}' '{group.Sid}' in context '{groupContext.ConnectedServer}' and '{groupContext.ContextType}'");

            foreach (Principal member in group.GetMembers(true))
            {
                if (member.SamAccountName == user.SamAccountName)
                {
                    logger.LogInfo(true, $"User already in group '{groupname}'");
                    return;
                }
            }

            group.Members.Add(user);
            group.Save();

            userContext.Dispose();
            groupContext.Dispose();

            logger.LogInfo(true, $"Added user '{userPrincipalName}' to group '{groupname}'");
        }

        /// <summary>
        /// Ensure user not in group
        /// </summary>
        /// <param name="userPrincipalName"></param>
        /// <param name="groupname"></param>
        /// <param name="logger"></param>
        /// <param name="acp"></param>
        public static void EnsureUserNotInGroup(
            string userPrincipalName,
            string groupname,
            ILoggerInterface logger,
            AccountManagementPrincipalContext acp)
        {
            logger.LogInfo(true, $"Ensure user '{userPrincipalName}' NOT in group {groupname}");

            UserPrincipal up = SearchUser(userPrincipalName, acp, out var userContext);

            if (up == null)
            {
                userContext?.Dispose();
                throw new Exception($"User '{userPrincipalName}' not found.");
            }

            GroupPrincipal group = SearchGroup(groupname, acp, out var groupContext);

            if (group == null)
            {
                userContext?.Dispose();
                groupContext?.Dispose();
                throw new Exception($"Group '{groupname}' not found.");
            }

            foreach (Principal member in group.GetMembers(true))
            {
                if (member.SamAccountName == up.SamAccountName)
                {
                    group.Members.Remove(member);
                    group.Save();
                    logger.LogInfo(true, $"Removed user '{userPrincipalName}' from group '{groupname}'");
                    return;
                }
            }

            logger.LogInfo(true, $"User '{userPrincipalName}' not found in group '{groupname}'");
        }

        /// <summary>
        /// Create a user or return one if it does not exist.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="password"></param>
        /// <param name="displayName"></param>
        /// <param name="logger"></param>
        /// <param name="acp"></param>
        /// <returns></returns>
        public static UserPrincipal EnsureUserExists(
            string identity,
            string password,
            string displayName,
            ILoggerInterface logger,
            AccountManagementPrincipalContext acp)
        {
            var parsedUserName = new FqdnNameParser(identity);

            if (parsedUserName.UserPrincipalName.Length > 64)
            {
                throw new Exception($"Windows account userPrincipalName '{parsedUserName.UserPrincipalName}' cannot be longer than 64 characters.");
            }

            using (PrincipalContext pc = BuildPrincipal(acp))
            {
                UserPrincipal up = FindUser(identity, pc);

                string samAccountName = parsedUserName.SamAccountName;

                logger.LogInfo(false, $"Ensure windows account exists '{samAccountName}@{password}' with userPrincipal '{identity}'");

                if (up == null)
                {
                    up = new UserPrincipal(pc, samAccountName, password, true);
                }
                else
                {
                    logger.LogInfo(true, $"Found account IsAccountLockedOut={up.IsAccountLockedOut()}, SamAccountName={up.SamAccountName}");

                    // Make sure we have the latest password, just in case
                    // the pwd algorithm generation changes...
                    try
                    {
                        up.SetPassword(password);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(true, "Cannot update password for account: " + e.Message + e.InnerException?.Message);
                    }
                }

                up.UserCannotChangePassword = true;
                up.PasswordNeverExpires = true;
                up.Enabled = true;
                up.DisplayName = displayName;
                up.Description = parsedUserName.UserPrincipalName;

                // If we are in a domain, assign the user principal name
                if (pc.ContextType == ContextType.Domain)
                {
                    logger.LogInfo(true, "Setting UserPrincipalName to '{0}'", parsedUserName.UserPrincipalName);
                    up.UserPrincipalName = parsedUserName.UserPrincipalName + "@" + parsedUserName.DomainName.ToLower();
                }

                if (up.IsAccountLockedOut())
                {
                    try
                    {
                        up.UnlockAccount();
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(true, "Cannot unlock account: " + e.Message + e.InnerException?.Message);
                    }
                }

                try
                {
                    up.Save();
                }
                catch (Exception e)
                {
                    logger.LogException(new Exception("Error while saving user", e), EventLogEntryType.Warning);

                    // Sometimes it crashes, but everything was OK (weird?)
                    // so we check again if the user has been created
                    Thread.Sleep(500);
                    up = FindUser(identity, pc);

                    if (up == null)
                    {
                        // Rethrow the original, whatever it was.
                        ExceptionDispatchInfo.Capture(e).Throw();
                    }
                }

                return up;
            }
        }

        /// <summary>
        /// Add a user to a group in a principal context
        /// </summary>
        /// <param name="up"></param>
        /// <param name="groupName"></param>
        /// <param name="pc"></param>
        private static void AddGroup(UserPrincipal up, string groupName, PrincipalContext pc)
        {
            using (GroupPrincipal gp = GroupPrincipal.FindByIdentity(pc, groupName))
            {
                if (!gp.Members.Contains(up))
                {
                    gp.Members.Add(up);
                    gp.Save();
                }
            }
        }

        private static Principal FindIdentity(string identity, PrincipalContext pc)
        {
            // Search for computer names is a little bit different...
            if (identity.EndsWith("$"))
            {
                var computer = FindComputer(identity, pc);
                return computer;
            }

            // Search in order user, group machine
            var user = FindUser(identity, pc);

            if (user != null)
            {
                return user;
            }

            var group = FindGroup(identity, pc);

            if (group != null)
            {
                return group;
            }

            return null;
        }

        /// <summary>
        /// Search a user hierarchicaly in the AD structure
        /// </summary>
        /// <param name="userName">The user name or identifier</param>
        /// <param name="ac">The principal context global configuration</param>
        /// <param name="pc">The principal context this user was found in</param>
        /// <returns></returns>
        private static UserPrincipal SearchUser(string userName, AccountManagementPrincipalContext ac, out PrincipalContext pc)
        {
            UserPrincipal user = null;

            var name = new FqdnNameParser(userName);

            if (name.ContextType == ContextType.Machine)
            {
                pc = BuildPrincipal(null);

                user = FindUser(userName, pc);

                if (user != null)
                {
                    return user;
                }

                pc.Dispose();

                return null;
            }

            // Probar sin OU
            pc = BuildPrincipal(ac);

            user = FindUser(userName, pc);

            if (user != null)
            {
                return user;
            }

            pc.Dispose();

            // Probar con OU
            pc = BuildPrincipalWithoutOu(ac);

            user = FindUser(userName, pc);

            if (user != null)
            {
                return user;
            }

            pc.Dispose();

            return null;
        }

        /// <summary>
        /// Search for a group in diferent principal context in the following order: local, domain without OU, domain with OU
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="ac"></param>
        /// <param name="pc"></param>
        /// <returns></returns>
        private static GroupPrincipal SearchGroup(string groupName, AccountManagementPrincipalContext ac, out PrincipalContext pc)
        {
            var name = new FqdnNameParser(groupName);

            GroupPrincipal group = null;

            if (name.ContextType == ContextType.Machine)
            {
                pc = BuildPrincipal(null);

                group = FindGroup(groupName, pc);

                if (group != null)
                {
                    return group;
                }

                pc.Dispose();

                return null;
            }

            // Look in provided OU
            pc = BuildPrincipal(ac);

            group = FindGroup(groupName, pc);

            if (group != null)
            {
                return group;
            }

            pc.Dispose();

            // Look in domain
            pc = BuildPrincipalWithoutOu(ac);

            group = FindGroup(groupName, pc);

            if (group != null)
            {
                return group;
            }

            pc.Dispose();
            return null;
        }

        /// <summary>
        /// Get a user in a principal context
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="pc"></param>
        /// <returns></returns>
        private static UserPrincipal FindUser(string userName, PrincipalContext pc)
        {
            UserPrincipal user;
            PrincipalSearcher searcher;

            var parsedName = new FqdnNameParser(userName);

            user = new UserPrincipal(pc);
            user.SamAccountName = parsedName.SamAccountName;
            searcher = new PrincipalSearcher(user);
            user = searcher.FindOne() as UserPrincipal;

            return user;
        }

        /// <summary>
        /// Get a group in a principal context
        /// </summary>
        /// <param name="groupName">The group name or identifier</param>
        /// <param name="pc">The principal context</param>
        /// <returns></returns>
        private static GroupPrincipal FindGroup(string groupName, PrincipalContext pc)
        {
            GroupPrincipal group;
            PrincipalSearcher searcher;

            var parsedName = new FqdnNameParser(groupName);

            // Search by SamGroupName
            if (!string.IsNullOrWhiteSpace(parsedName.SamAccountName))
            {
                group = new GroupPrincipal(pc);
                group.SamAccountName = parsedName.SamAccountName;
                searcher = new PrincipalSearcher(group);
                group = searcher.FindOne() as GroupPrincipal;

                if (group != null)
                {
                    return group;
                }
            }

            // Search by name (original approach, backwards compatiblity)
            if (!string.IsNullOrWhiteSpace(parsedName.SamAccountName))
            {
                group = new GroupPrincipal(pc);
                group.Name = parsedName.SamAccountName;
                searcher = new PrincipalSearcher(group);
                group = searcher.FindOne() as GroupPrincipal;

                if (group != null)
                {
                    return group;
                }
            }

            if (parsedName.Sid != null)
            {
                group = GroupPrincipal.FindByIdentity(pc, IdentityType.Sid, parsedName.Sid.ToString());

                if (group != null)
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pc"></param>
        /// <returns></returns>
        private static ComputerPrincipal FindComputer(string name, PrincipalContext pc)
        {
            ComputerPrincipal group;
            PrincipalSearcher searcher;

            // Specify computer name with dolar sign at the end
            name = name.Trim("$".ToCharArray());

            // Search by Name
            group = new ComputerPrincipal(pc);
            group.Name = name;
            searcher = new PrincipalSearcher(group);
            group = searcher.FindOne() as ComputerPrincipal;

            if (group != null)
            {
                return group;
            }

            // Search by SamGroupName
            group = new ComputerPrincipal(pc);
            group.SamAccountName = name;
            searcher = new PrincipalSearcher(group);
            group = searcher.FindOne() as ComputerPrincipal;

            if (group != null)
            {
                return group;
            }

            return null;
        }
    }
}
