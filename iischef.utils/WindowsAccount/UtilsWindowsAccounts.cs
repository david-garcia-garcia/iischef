using iischef.logger;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace iischef.utils.WindowsAccount
{
    /// <summary>
    /// Utils to handle permission management
    /// </summary>
    public static class UtilsWindowsAccounts
    {
        // https://support.microsoft.com/en-us/help/243330/well-known-security-identifiers-in-windows-operating-systems
        // https://ldapwiki.com/wiki/Well-known%20Security%20Identifiers#:~:text=Well%2Dknown%20Security%20Identifiers%20(ObjectSID,all%20Microsoft%20Windows%20Operating%20Systems.&text=No%20security%20principal.&text=An%20identifier%20authority.

        public const string WELL_KNOWN_SID_EVERYONE = "S-1-1-0";

        public const string WELL_KNOWN_SID_USERS = "S-1-5-32-545";

        public const string WELL_KNOWN_SID_IIS_USERS = "S-1-5-32-568";

        public const string WELL_KNOWN_SID_AUTHENTICATED_USERS = "S-1-5-11";

        public const string WELL_KNOWN_SID_LOCAL_SYSTEM = "S-1-5-18";

        public const string WELL_KNOWN_SID_ADMINISTRATORS = "S-1-5-32-544";

        /// <summary>
        /// Add this permission to a directory and user
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="directory"></param>
        /// <param name="fileSystemRights"></param>
        /// <param name="acp"></param>
        /// <param name="logger"></param>
        /// <param name="type"></param>
        public static void SetUniqueAclForIdentity(
            string identity,
            string directory,
            FileSystemRights fileSystemRights,
            ILoggerInterface logger,
            AccessControlType type = AccessControlType.Allow)
        {
            SetUniqueAclForIdentity(
                directory,
                logger,
                new List<AclInfo>()
                {
                    new AclInfo()
                    {
                        Sid = new SecurityIdentifier(identity),
                        FileSystem = fileSystemRights,
                        AccessControlType = type
                    }
                });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="logger"></param>
        /// <param name="aclInfo"></param>
        /// <param name="pc"></param>
        public static void SetUniqueAclForIdentity(
            string directory,
            ILoggerInterface logger,
            List<AclInfo> aclInfo)
        {
            var directoryInfo = new DirectoryInfo(directory);

            DirectorySecurity dSecurity = directoryInfo.GetAccessControl();

            bool changed = false;

            // Acls to Add
            foreach (var acl in aclInfo.Where((i) => i.Remove == false))
            {
                dSecurity.SetAclForIdentity(acl.Sid, acl.FileSystem, acl.AccessControlType, ref changed);
            }

            // Acl to Remove
            foreach (var acl in aclInfo.Where((i) => i.Remove == true))
            {
                dSecurity.RemoveAllAccessRulesForIdentity(acl.Sid, ref changed);
            }

            if (changed)
            {
                bool smallDir = UtilsSystem.IsSmallDirectory(directoryInfo.FullName, 2000);

                // Only warn if we have > threshold files/directories.
                if (!smallDir)
                {
                    logger.LogWarning(false, "New ACL will be applied to path: {0}. This might take a very long time depending on the contents of this directory.", directoryInfo.FullName);
                }

                directoryInfo.SetAccessControl(dSecurity);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public class AclInfo
        {
            /// <summary>
            /// Indicates that any ACL rule for the identity in
            /// this AclInfo should be removed
            /// </summary>
            public bool Remove { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public SecurityIdentifier Sid { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public FileSystemRights FileSystem { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public AccessControlType AccessControlType { get; set; }
        }
    }
}
