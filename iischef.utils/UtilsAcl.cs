using iischef.logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace iischef.utils
{
    /// <summary>
    /// 
    /// </summary>
    public static class UtilsAcl
    {
        /// <summary>
        /// Ensure that the Principal identified by "identity"
        /// has only an access rule explicitly set on the dSecurity
        /// object with the given rights and type. If any other
        /// rights/type existed for this identity, they will be replaced.
        /// </summary>
        public static DirectorySecurity SetAclForIdentity(
            this DirectorySecurity dSecurity,
            IdentityReference identity,
            FileSystemRights rights,
            AccessControlType type,
            ref bool changed)
        {
            var rules = dSecurity.GetAccessRules(
                true,
                true,
                typeof(SecurityIdentifier));

            List<FileSystemAccessRule> nonInheritedRulesForIdentity = new List<FileSystemAccessRule>();

            foreach (AuthorizationRule r in rules)
            {
                if (r.IdentityReference == identity
                    && r is FileSystemAccessRule fileSystemAccessRule
                    && r.IsInherited == false)
                {
                    nonInheritedRulesForIdentity.Add(fileSystemAccessRule);
                }
            }

            FileSystemAccessRule accessRule = new FileSystemAccessRule(
                             identity,
                             fileSystemRights: rights,
                             inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                             propagationFlags: PropagationFlags.None,
                             type: type);

            FileSystemAccessRule matchingRule = null;

            // If there is an existing rule that completely matches our configuration,
            // do nothing as SetAccessControl() calls can be extremely slow.
            foreach (var currentRule in nonInheritedRulesForIdentity)
            {
                if (currentRule != null
                    && currentRule.FileSystemRights == accessRule.FileSystemRights
                    && currentRule.InheritanceFlags == accessRule.InheritanceFlags
                    && currentRule.PropagationFlags == accessRule.PropagationFlags
                    && currentRule.AccessControlType == accessRule.AccessControlType)
                {
                    matchingRule = currentRule;
                    break;
                }
            }

            foreach (var currentRule in nonInheritedRulesForIdentity)
            {
                if (matchingRule == currentRule)
                {
                    // Do not remove
                    continue;
                }

                changed = true;
                dSecurity.RemoveAccessRule(currentRule);
            }

            // Add the FileSystemAccessRule to the security settings. 
            if (matchingRule == null)
            {
                changed = true;
                dSecurity.AddAccessRule(accessRule);
            }

            return dSecurity;
        }

        /// <summary>
        /// Remove any explicit permissions for an identity on a directory if they exist.
        /// </summary>
        public static void RemoveAllAccessRulesForIdentity(
            this DirectorySecurity dSecurity,
            IdentityReference identity,
            ref bool changed)
        {
            var rules = dSecurity.GetAccessRules(
                true,
                false,
                typeof(SecurityIdentifier));

            foreach (AuthorizationRule r in rules)
            {
                if (r.IdentityReference == identity)
                {
                    var currentRule = (FileSystemAccessRule)r;
                    dSecurity.RemoveAccessRule(currentRule);
                    changed = true;
                }
            }
        }
    }
}
