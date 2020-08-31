using System;
using System.Runtime.InteropServices;
using System.Text;

namespace iischef.utils
{
    public static class Advapi32Extern
    {
        // Import the LSA functions
        public const int Access = (int)(
            LSA_AccessPolicy.POLICY_AUDIT_LOG_ADMIN |
            LSA_AccessPolicy.POLICY_CREATE_ACCOUNT |
            LSA_AccessPolicy.POLICY_CREATE_PRIVILEGE |
            LSA_AccessPolicy.POLICY_CREATE_SECRET |
            LSA_AccessPolicy.POLICY_GET_PRIVATE_INFORMATION |
            LSA_AccessPolicy.POLICY_LOOKUP_NAMES |
            LSA_AccessPolicy.POLICY_NOTIFICATION |
            LSA_AccessPolicy.POLICY_SERVER_ADMIN |
            LSA_AccessPolicy.POLICY_SET_AUDIT_REQUIREMENTS |
            LSA_AccessPolicy.POLICY_SET_DEFAULT_QUOTA_LIMITS |
            LSA_AccessPolicy.POLICY_TRUST_ADMIN |
            LSA_AccessPolicy.POLICY_VIEW_AUDIT_INFORMATION |
            LSA_AccessPolicy.POLICY_VIEW_LOCAL_INFORMATION);

        [DllImport("advapi32.dll", PreserveSig = true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "It's needed Case Sensitivity")]
        public static extern int LsaOpenPolicy(
            ref LSA_UNICODE_STRING SystemName,
            ref LSA_OBJECT_ATTRIBUTES ObjectAttributes,
            int DesiredAccess,
            out IntPtr PolicyHandle);

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "It's needed Case Sensitivity")]
        public static extern int LsaAddAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            LSA_UNICODE_STRING[] UserRights,
            int CountOfRights);

        [DllImport("advapi32")]
        public static extern void FreeSid(IntPtr pSid);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true, PreserveSig = true)]
        public static extern bool LookupAccountName(
            string lpSystemName, 
            string lpAccountName,
            IntPtr psid,
            ref int cbsid,
            StringBuilder domainName, 
            ref int cbdomainLength, 
            ref int use);

        [DllImport("advapi32.dll")]
        public static extern bool IsValidSid(IntPtr pSid);

        [DllImport("advapi32.dll")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "It's needed Case Sensitivity")]
        public static extern int LsaClose(IntPtr ObjectHandle);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        [DllImport("advapi32.dll")]
        public static extern int LsaNtStatusToWinError(int status);

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "It's needed Case Sensitivity")]
        public static extern int LsaEnumerateAccountRights(IntPtr PolicyHandle, IntPtr AccountSid, out IntPtr UserRightsPtr, out int CountOfRights);

        // define the structures

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public LSA_UNICODE_STRING ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        // enum all policies

        public enum LSA_AccessPolicy : long
        {
            POLICY_VIEW_LOCAL_INFORMATION = 0x00000001L,
            POLICY_VIEW_AUDIT_INFORMATION = 0x00000002L,
            POLICY_GET_PRIVATE_INFORMATION = 0x00000004L,
            POLICY_TRUST_ADMIN = 0x00000008L,
            POLICY_CREATE_ACCOUNT = 0x00000010L,
            POLICY_CREATE_SECRET = 0x00000020L,
            POLICY_CREATE_PRIVILEGE = 0x00000040L,
            POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080L,
            POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100L,
            POLICY_AUDIT_LOG_ADMIN = 0x00000200L,
            POLICY_SERVER_ADMIN = 0x00000400L,
            POLICY_LOOKUP_NAMES = 0x00000800L,
            POLICY_NOTIFICATION = 0x00001000L
        }
    }
}
