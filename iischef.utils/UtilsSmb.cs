using iischef.logger;
using Microsoft.Management.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace iischef.utils
{
    public static class UtilsSmb
    {
        /// <summary>
        /// 
        /// </summary>
        public enum SmbAccessRight
        {
            /// <summary>
            /// Full
            /// </summary>
            Full = 0,

            /// <summary>
            /// Change
            /// </summary>
            Change = 1,

            /// <summary>
            /// Read
            /// </summary>
            Read = 2
        }

        /// <summary>
        /// 
        /// </summary>
        public enum SmbAccessControlType
        {
            /// <summary>
            /// Deny
            /// </summary>
            Deny = 1,

            /// <summary>
            /// Grant
            /// </summary>
            Grant = 0
        }

        /// <summary>
        /// Get SMB share info. Returns NULL if the share does not exist.
        /// </summary>
        /// <param name="shareName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static SmbShareDTO GetSmbShare(string shareName, ILoggerInterface logger)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddCommand("Get-SmbShare");
                ps.AddParameter("Name", shareName);

                try
                {
                    var results = ps.InvokeAndTreatError(logger);

                    if (results.Any())
                    {
                        var smbShare = (CimInstance)results.First().BaseObject;

                        if (smbShare != null)
                        {
                            return new SmbShareDTO
                            {
                                Name = smbShare.CimInstanceProperties["Name"].Value as string,
                                Path = smbShare.CimInstanceProperties["Path"].Value as string,
                                Description = smbShare.CimInstanceProperties["Description"].Value as string,
                                EncryptData = (bool?)smbShare.CimInstanceProperties["EncryptData"].Value ?? false,
                                ContinuousAvailability = (bool?)smbShare.CimInstanceProperties["ContinuouslyAvailable"].Value ?? false,
                                ConcurrentUserLimit = (uint?)smbShare.CimInstanceProperties["ConcurrentUserLimit"].Value
                            };
                        }
                    }

                    return null;
                }
                catch (ErrorRecordException err) when (err.ErrorRecord?.CategoryInfo?.Category == ErrorCategory.ObjectNotFound)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="shareName"></param>
        /// <param name="logger"></param>
        public static void RevokeShareAccess(string identity, string shareName, ILoggerInterface logger)
        {
            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Revoke-SmbShareAccess");
                ps.AddParameter("AccountName", identity);
                ps.AddParameter("Name", shareName);
                ps.AddParameter("Force", true);
                ps.InvokeAndTreatError(logger);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="shareName"></param>
        /// <param name="accessRight"></param>
        /// <param name="accessInfos"></param>
        /// <param name="logger"></param>
        public static void GrantShareAccess(
            string identity,
            string shareName,
            SmbAccessRight accessRight,
            List<SmbShareAccessInfo> accessInfos,
            ILoggerInterface logger)
        {
            if (accessInfos.Any((i) => i.AccountName == identity && i.AccessRight == accessRight))
            {
                return;
            }

            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Grant-SmbShareAccess");
                ps.AddParameter("AccountName", identity);
                ps.AddParameter("Name", shareName);
                ps.AddParameter("AccessRight", accessRight);
                ps.AddParameter("Confirm", false);
                ps.InvokeAndTreatError(logger);
            }

            // Permissions have changed, so we need to re-do the passed in access-infos
            accessInfos.Clear();
            accessInfos.AddRange(UtilsSmb.GetShareAccessInfo(shareName, logger));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shareName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static List<SmbShareAccessInfo> GetShareAccessInfo(string shareName, ILoggerInterface logger)
        {
            var accessInfoList = new List<SmbShareAccessInfo>();

            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Get-SmbShareAccess");
                ps.AddParameter("Name", shareName);

                var results = ps.InvokeAndTreatError(logger);

                foreach (var result in results)
                {
                    var accountName = result.Members["AccountName"].Value.ToString();
                    var accessRight = result.Members["AccessRight"].Value.ToString();
                    var accessControlType = result.Members["AccessControlType"].Value.ToString();
                    var scopeName = result.Members["ScopeName"].Value.ToString();

                    accessInfoList.Add(new SmbShareAccessInfo()
                    {
                        AccessRight = (SmbAccessRight)int.Parse(accessRight),
                        AccountName = accountName,
                        ScopeName = scopeName,
                        AccessControlType = (SmbAccessControlType)int.Parse(accessControlType)
                    });
                }
            }

            return accessInfoList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shareName"></param>
        /// <param name="path"></param>
        /// <param name="logger"></param>
        public static void CreateSmbShare(string shareName, string path, ILoggerInterface logger)
        {
            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("New-SmbShare");
                ps.AddParameter("Path", path);
                ps.AddParameter("Name", shareName);
                ps.InvokeAndTreatError(logger);
            }

            // WindowPrivilegeUtils.LookupAccountName(UtilsWindowsAccounts.WELL_KNOWN_SID_EVERYONE, out string accountName, out string domainName);

            // Remove everyone, which is added by default to any new share
            // RevokeShareAccess(accountName, shareName, logger);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shareName"></param>
        /// <param name="logger"></param>
        public static void DeleteSmbShare(string shareName, ILoggerInterface logger)
        {
            // Removing the existing share and recreating it with the new path
            // Note: This approach may have downtime implications
            using (PowerShell ps = PowerShell.Create())
            {
                // Remove the existing share
                ps.AddCommand("Remove-SmbShare");
                ps.AddParameter("Name", shareName);
                ps.AddParameter("Confirm", false);
                ps.InvokeAndTreatError(logger);
            }
        }

        public class SmbShareDTO
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Description { get; set; }
            public bool EncryptData { get; set; }
            public bool ContinuousAvailability { get; set; }
            public uint? ConcurrentUserLimit { get; set; }
        }

        public class SmbShareAccessInfo
        {
            public string AccountName { get; set; }

            public SmbAccessRight AccessRight { get; set; }

            public string ScopeName { get; set; }

            public SmbAccessControlType AccessControlType { get; set; }
        }
    }
}
