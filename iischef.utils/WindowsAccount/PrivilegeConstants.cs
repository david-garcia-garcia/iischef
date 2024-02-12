namespace iischef.utils.WindowsAccount
{
    /// <summary>
    /// 
    /// </summary>
    public static class PrivilegeConstants
    {
        // Required to assign the primary token of a process.
        public const string SeAssignPrimaryTokenPrivilege = "SeAssignPrimaryTokenPrivilege";

        // Required to generate audit-log entries.
        public const string SeAuditPrivilege = "SeAuditPrivilege";

        // Required to receive notifications of changes to files or directories.
        public const string SeChangeNotifyPrivilege = "SeChangeNotifyPrivilege";

        // Required to create named file mapping objects in the global namespace during Terminal Services sessions.
        public const string SeCreateGlobalPrivilege = "SeCreateGlobalPrivilege";

        // Required to create a pagefile.
        public const string SeCreatePagefilePrivilege = "SeCreatePagefilePrivilege";

        // Required to create a permanent object.
        public const string SeCreatePermanentPrivilege = "SeCreatePermanentPrivilege";

        // Required to create a symbolic link.
        public const string SeCreateSymbolicLinkPrivilege = "SeCreateSymbolicLinkPrivilege";

        // Required to create a token object.
        public const string SeCreateTokenPrivilege = "SeCreateTokenPrivilege";

        // Required to debug programs.
        public const string SeDebugPrivilege = "SeDebugPrivilege";

        // Required to enable computer and user accounts to be trusted for delegation.
        public const string SeEnableDelegationPrivilege = "SeEnableDelegationPrivilege";

        // Required to increase the base priority of a process.
        public const string SeIncreaseBasePriorityPrivilege = "SeIncreaseBasePriorityPrivilege";

        // Required to increase the quota assigned to a process.
        public const string SeIncreaseQuotaPrivilege = "SeIncreaseQuotaPrivilege";

        // Required to allocate memory in another process.
        public const string SeIncreaseWorkingSetPrivilege = "SeIncreaseWorkingSetPrivilege";

        // Required to load and unload device drivers.
        public const string SeLoadDriverPrivilege = "SeLoadDriverPrivilege";

        // Required to lock pages in memory.
        public const string SeLockMemoryPrivilege = "SeLockMemoryPrivilege";

        // Required to bypass traverse checking.
        public const string SeManageVolumePrivilege = "SeManageVolumePrivilege";

        // Required to profile the performance of the system.
        public const string SeSystemProfilePrivilege = "SeSystemProfilePrivilege";

        // Required to modify the system time.
        public const string SeSystemtimePrivilege = "SeSystemtimePrivilege";

        // Required to perform system-wide operations on the computer.
        public const string SeSystemEnvironmentPrivilege = "SeSystemEnvironmentPrivilege";

        // Required to shut down the system.
        public const string SeShutdownPrivilege = "SeShutdownPrivilege";

        // Required to synchronize directory service data.
        public const string SeSyncAgentPrivilege = "SeSyncAgentPrivilege";

        // Required to modify the system time zone.
        public const string SeTimeZonePrivilege = "SeTimeZonePrivilege";

        // Required to access Credential Manager as a trusted caller.
        public const string SeTrustedCredManAccessPrivilege = "SeTrustedCredManAccessPrivilege";

        // Required to create a computer account.
        public const string SeCreateComputerAccountPrivilege = "SeCreateComputerAccountPrivilege";

        // Required to act as the local system.
        public const string SeImpersonatePrivilege = "SeImpersonatePrivilege";

        // Required to modify the access control lists (ACL) of an object.
        public const string SeSecurityPrivilege = "SeSecurityPrivilege";

        // Required to restore files and directories.
        public const string SeRestorePrivilege = "SeRestorePrivilege";

        // Required to access the computer from the network.
        public const string SeNetworkLogonRight = "SeNetworkLogonRight";

        // Required to log on as a service.
        public const string SeServiceLogonRight = "SeServiceLogonRight";

        // Required to shut down a system using a network request.
        public const string SeRemoteShutdownPrivilege = "SeRemoteShutdownPrivilege";

        // Required to take ownership of an object.
        public const string SeTakeOwnershipPrivilege = "SeTakeOwnershipPrivilege";
    }
}
