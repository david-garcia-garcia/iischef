using System;
using System.IO;
using System.Runtime.InteropServices;

namespace iischef.utils
{
    public static class SymlinkUtils
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr lpSecurityAttributes,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetFinalPathNameByHandle(
            IntPtr hFile,
            [MarshalAs(UnmanagedType.LPTStr)] System.Text.StringBuilder lpszFilePath,
            int cchFilePath,
            int dwFlags);

        public static string GetRealPath(string symlinkPath)
        {
            var handle = CreateFile(symlinkPath, 0, 2, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);

            if (handle.ToInt32() == -1)
            {
                throw new IOException($"Failed to open symlink '{symlinkPath}'.");
            }

            try
            {
                var sb = new System.Text.StringBuilder(1024);
                var result = GetFinalPathNameByHandle(handle, sb, sb.Capacity, 0);

                if (result > 0)
                {
                    var realPath = sb.ToString().Substring(4); // remove the "\\?\" prefix
                    return realPath;
                }
                else
                {
                    throw new IOException($"Failed to get final path for symlink '{symlinkPath}'.");
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
