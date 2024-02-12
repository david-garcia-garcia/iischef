using iischef.logger;
using Microsoft.Win32.SafeHandles;
using NCode.ReparsePoints;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace iischef.utils
{
    /// <summary>
    /// Provides access to NTFS junction points in .Net.
    /// </summary>
    public static class UtilsJunction
    {
        /// <summary>
        /// The file or directory is not a reparse point.
        /// </summary>
        private const int ERROR_NOT_A_REPARSE_POINT = 4390;

        /// <summary>
        /// The reparse point attribute cannot be set because it conflicts with an existing attribute.
        /// </summary>
        private const int ERROR_REPARSE_ATTRIBUTE_CONFLICT = 4391;

        /// <summary>
        /// The data present in the reparse point buffer is invalid.
        /// </summary>
        private const int ERROR_INVALID_REPARSE_DATA = 4392;

        /// <summary>
        /// The tag present in the reparse point buffer is invalid.
        /// </summary>
        private const int ERROR_REPARSE_TAG_INVALID = 4393;

        /// <summary>
        /// There is a mismatch between the tag specified in the request and the tag present in the reparse point.
        /// </summary>
        private const int ERROR_REPARSE_TAG_MISMATCH = 4394;

        /// <summary>
        /// Command to set the reparse point data block.
        /// </summary>
        private const int FSCTL_SET_REPARSE_POINT = 0x000900A4;

        /// <summary>
        /// Command to get the reparse point data block.
        /// </summary>
        private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;

        /// <summary>
        /// Command to delete the reparse point data base.
        /// </summary>
        private const int FSCTL_DELETE_REPARSE_POINT = 0x000900AC;

        /// <summary>
        /// Reparse point tag used to identify mount points and junction points.
        /// </summary>
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

        /// <summary>
        /// This prefix indicates to NTFS that the path is to be treated as a non-interpreted
        /// path in the virtual file system.
        /// </summary>
        private const string NonInterpretedPathPrefix = @"\??\";

        [Flags]
        private enum EFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,
        }

        [Flags]
        private enum EFileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004,
        }

        private enum ECreationDisposition : uint
        {
            New = 1,
            CreateAlways = 2,
            OpenExisting = 3,
            OpenAlways = 4,
            TruncateExisting = 5,
        }

        [Flags]
        private enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [Flags]
        private enum ELinkTarget : uint
        {
            File = 0x00000000,
            Directory = 0x10000000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_DATA_BUFFER
        {
            /// <summary>
            /// Reparse point tag. Must be a Microsoft reparse point tag.
            /// </summary>
            public uint ReparseTag;

            /// <summary>
            /// Size, in bytes, of the data after the Reserved member. This can be calculated by:
            /// (4 * sizeof(ushort)) + SubstituteNameLength + PrintNameLength + 
            /// (namesAreNullTerminated ? 2 * sizeof(char) : 0);
            /// </summary>
            public ushort ReparseDataLength;

            /// <summary>
            /// Reserved; do not use. 
            /// </summary>
            public ushort Reserved;

            /// <summary>
            /// Offset, in bytes, of the substitute name string in the PathBuffer array.
            /// </summary>
            public ushort SubstituteNameOffset;

            /// <summary>
            /// Length, in bytes, of the substitute name string. If this string is null-terminated,
            /// SubstituteNameLength does not include space for the null character.
            /// </summary>
            public ushort SubstituteNameLength;

            /// <summary>
            /// Offset, in bytes, of the print name string in the PathBuffer array.
            /// </summary>
            public ushort PrintNameOffset;

            /// <summary>
            /// Length, in bytes, of the print name string. If this string is null-terminated,
            /// PrintNameLength does not include space for the null character. 
            /// </summary>
            public ushort PrintNameLength;

            /// <summary>
            /// A buffer containing the unicode-encoded path string. The path string contains
            /// the substitute name string and print name string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr inBuffer,
            int nInBufferSize,
            IntPtr outBuffer,
            int nOutBufferSize,
            out int pBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            EFileAccess dwDesiredAccess,
            EFileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            ECreationDisposition dwCreationDisposition,
            EFileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        /// <summary>
        /// Given a path, it will backwards resolve any junctions...
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var linkmanager = ReparsePointFactory.Create();

            // Partimos la ruta en trozos, nos da igual que sea fichero o directorio, y que exista o no una parte de la ruta
            var directoryParts = path.Split(Path.DirectorySeparatorChar).ToList();
            var finalDirectoryParts = new List<string>();

            while (directoryParts.Any())
            {
                var currentPath = string.Join(Path.DirectorySeparatorChar.ToString(), directoryParts);

                if (Directory.Exists(currentPath) || File.Exists(currentPath))
                {
                    var type = linkmanager.GetLinkType(currentPath);

                    if (type == LinkType.Junction
                        || type == LinkType.Symbolic)
                    {
                        string target = null;

                        // Ok this is it!
                        try
                        {
                            var atts = linkmanager.GetLink(currentPath);
                            target = atts.Target;

                            if (target.StartsWith("UNC\\"))
                            {
                                target = "\\" + target.Substring(3, target.Length - 3);
                            }

                            target = ExpandSmb(target);
                        }
                        catch (Win32Exception win32Exception) when (win32Exception.NativeErrorCode == 5)
                        {
                            target = SymlinkUtils.GetRealPath(currentPath);
                        }

                        // The target becomes the new remaining path
                        directoryParts = target.Split(Path.DirectorySeparatorChar).ToList();
                    }
                }

                finalDirectoryParts.Add(directoryParts.Last());
                directoryParts.RemoveAt(directoryParts.Count - 1);
            }

            finalDirectoryParts.Reverse();

            return string.Join("\\", finalDirectoryParts);
        }

        /// <summary>
        /// Expands any local SMB as if it were a junction
        /// </summary>
        /// <param name="path"></param>
        private static string ExpandSmb(string path)
        {
            if (!path.StartsWith($"\\\\{Environment.MachineName}\\"))
            {
                return path;
            }

            var shareName = Regex.Match(path, $"^\\\\\\\\{Regex.Escape(Environment.MachineName)}\\\\([^\\\\]+)(.*)$");

            var shareInfo = UtilsSmb.GetSmbShare(shareName.Groups[1].Value, null);

            if (shareInfo == null)
            {
                return path;
            }

            return shareInfo.Path + shareName.Groups[2].Value;
        }

        /// <summary>
        /// If path is a junction or symlink, remove it.
        /// </summary>
        /// <param name="path"></param>
        public static void RemoveJunction(string path)
        {
            var linkmanager = ReparsePointFactory.Create();

            // On a local folder based deployment we might be redeploying on top of same application...
            if (Directory.Exists(path))
            {
                var type = linkmanager.GetLinkType(path);

                if (type == LinkType.Junction
                    || type == LinkType.Symbolic
                    || type == LinkType.HardLink)
                {
                    UtilsJunction.DeleteJunctionOrSymlink(path);
                }
            }
        }

        /// <summary>
        /// Link type
        /// </summary>
        public enum LinkTypeRequest
        {
            /// <summary>
            /// Auto
            /// </summary>
            Auto = 0,

            /// <summary>
            /// Symlink
            /// </summary>
            Symlink = 1,

            /// <summary>
            /// Junction
            /// </summary>
            Junction = 2
        }

        public static FileSystemInfo GetInfo(string path)
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path);
            }

            if (File.Exists(path))
            {
                return new FileInfo(path);
            }

            return null;
        }

        /// <summary>
        /// Creates a link (symlink or junction)
        /// </summary>
        /// <param name="mountPath">Path where the symlink or junction will be created.</param>
        /// <param name="mountDestination">Path the JUNCTION points to.</param>
        /// <param name="logger"></param>
        /// <param name="persistOnDeploy">If true, any files in the repo are synced to the content folder.</param>
        /// <param name="overWrite">If a junction or link already exists, overwrite it</param>
        /// <param name="linkType">Use this to force usage of symlinks. Otherwise junction/symlink is chosen by the method internally.</param>
        /// <returns></returns>
        public static void EnsureLink(
            string mountPath,
            string mountDestination,
            ILoggerInterface logger,
            bool persistOnDeploy,
            bool overWrite = false,
            LinkTypeRequest linkType = LinkTypeRequest.Auto)
        {
            var linkmanager = ReparsePointFactory.Create();

            var existingTargetInfo = GetInfo(mountPath);

            // On a local folder based deployment we might be redeploying on top of same application...
            if (existingTargetInfo != null)
            {
                logger.LogWarning(true, "Mount destination already exists: {0}", mountPath);

                if (linkmanager.GetLinkType(mountPath) == LinkType.Junction
                    || linkmanager.GetLinkType(mountPath) == LinkType.Symbolic)
                {
                    logger.LogInfo(true, "Mount destination is junction, grabbing attributes.");
                    var atts = linkmanager.GetLink(mountPath);
                    logger.LogInfo(true, "Mount destination attributes: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(atts));

                    var currentTarget = GetInfo(atts.Target);
                    var requiredTarget = GetInfo(mountDestination);

                    // It already exists, and matches the desired target
                    if (currentTarget.FullName == requiredTarget.FullName)
                    {
                        return;
                    }

                    if (overWrite)
                    {
                        existingTargetInfo.Delete();
                    }
                    else
                    {
                        // Something already exists. And it is NOT a junction equivalent to what
                        // we are asking for.
                        throw new Exception($"Could not mount junction because a directory or junction already exists at the junction source path: {mountPath}");
                    }
                }
                else if (existingTargetInfo is DirectoryInfo existingTargetDirectoryInfo)
                {
                    bool existingDirectoryHasFiles = Directory.EnumerateFiles(mountPath, "*", SearchOption.AllDirectories).Any();

                    // If the mountpath exists, but has nothing in it, delete it to make this process more error-proof.
                    if (Directory.Exists(mountPath))
                    {
                        if (persistOnDeploy)
                        {
                            // Copy any files, and then delete
                            UtilsSystem.CopyFilesRecursivelyFast(mountPath, mountDestination, true, null, logger);
                            Directory.Delete(mountPath, true);
                        }
                        else
                        {
                            if (!existingDirectoryHasFiles)
                            {
                                // Delete so we can junction
                                Directory.Delete(mountPath, true);
                            }
                        }
                    }
                }
            }

            // Create junction will fail if the physicial folder exists at the junction target
            // so the previous logic takes care of that...7
            bool useSymlinkInsteadOfJunction;

            switch (linkType)
            {
                case LinkTypeRequest.Auto:
                    // Use symbolink if target is a file, or a network path.
                    useSymlinkInsteadOfJunction = mountDestination.StartsWith("\\") || File.Exists(mountDestination);
                    break;
                case LinkTypeRequest.Junction:
                    useSymlinkInsteadOfJunction = false;
                    break;
                case LinkTypeRequest.Symlink:
                    useSymlinkInsteadOfJunction = true;
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (linkType == LinkTypeRequest.Junction && File.Exists(mountDestination))
            {
                throw new Exception("Junctions cannot be used to map files. They only work with directories.");
            }

            logger.LogInfo(true, $"Creating {(useSymlinkInsteadOfJunction ? "symlink" : "junction")} '{mountPath}' => '{mountDestination}'");

            var finalLinkType = useSymlinkInsteadOfJunction ? LinkType.Symbolic : LinkType.Junction;

            // For remote drives, junctions will not work
            // https://helpcenter.netwrix.com/Configure_IT_Infrastructure/File_Servers/Enable_Symlink.html
            try
            {
                linkmanager.CreateLink(mountPath, mountDestination, finalLinkType);
            }
            catch (Exception e)
            {
                throw new Exception($"Error '{e.Message}' creating {finalLinkType} '{mountPath}' -> '{mountDestination}'", e);
            }

            var link = linkmanager.GetLink(mountPath);
        }

        /// <summary>
        /// Deletes a junction point at the specified source directory along with the directory itself.
        /// Does nothing if the junction point does not exist.
        /// </summary>
        /// <remarks>
        /// Only works on NTFS.
        /// </remarks>
        /// <param name="junctionPoint">The junction point path</param>
        public static void DeleteJunctionOrSymlink(string junctionPoint)
        {
            if (!Directory.Exists(junctionPoint))
            {
                if (File.Exists(junctionPoint))
                {
                    throw new IOException("Path is not a junction point.");
                }

                return;
            }

            var linkManager = ReparsePointFactory.Create();

            if (linkManager.GetLinkType(junctionPoint) == LinkType.Symbolic)
            {
                Directory.Delete(junctionPoint, false);
                return;
            }

            using (SafeFileHandle handle = OpenReparsePoint(junctionPoint, EFileAccess.GenericWrite))
            {
                REPARSE_DATA_BUFFER reparseDataBuffer = new REPARSE_DATA_BUFFER();

                reparseDataBuffer.ReparseTag = IO_REPARSE_TAG_MOUNT_POINT;
                reparseDataBuffer.ReparseDataLength = 0;
                reparseDataBuffer.PathBuffer = new byte[0x3ff0];

                int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try
                {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    bool result = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_DELETE_REPARSE_POINT, inBuffer, 8, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                    if (!result)
                    {
                        ThrowLastWin32Error("Unable to delete junction point.");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuffer);
                }

                try
                {
                    Directory.Delete(junctionPoint);
                }
                catch (IOException ex)
                {
                    throw new IOException("Unable to delete junction point.", ex);
                }
            }
        }

        /// <summary>
        /// Determines whether the specified path exists and refers to a junction point.
        /// </summary>
        /// <param name="path">The junction point path</param>
        /// <returns>True if the specified path represents a junction point</returns>
        /// <exception cref="IOException">Thrown if the specified path is invalid
        /// or some other error occurs</exception>
        public static bool IsJunctionOrSymlink(string path)
        {
            var linkmanager = ReparsePointFactory.Create();
            var linkType = linkmanager.GetLinkType(path);
            return linkType == LinkType.Junction || linkType == LinkType.Symbolic;
        }

        private static SafeFileHandle OpenReparsePoint(string reparsePoint, EFileAccess accessMode)
        {
            SafeFileHandle reparsePointHandle = new SafeFileHandle(
                CreateFile(
                    reparsePoint,
                    accessMode,
                    EFileShare.Read | EFileShare.Write | EFileShare.Delete,
                    IntPtr.Zero,
                    ECreationDisposition.OpenExisting,
                    EFileAttributes.BackupSemantics | EFileAttributes.OpenReparsePoint,
                    IntPtr.Zero), true);

            if (Marshal.GetLastWin32Error() != 0)
            {
                ThrowLastWin32Error("Unable to open reparse point.");
            }

            return reparsePointHandle;
        }

        private static void ThrowLastWin32Error(string message)
        {
            throw new IOException(message, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }
    }
}