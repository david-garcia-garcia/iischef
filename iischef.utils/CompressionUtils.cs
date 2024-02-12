using System;
using System.IO;

namespace iischef.utils
{
    public static class CompressionUtils
    {
        /// <summary>
        /// Extract file using 7ZIP
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="targetDirectory"></param>
        /// <param name="password"></param>
        /// <param name="cancellationToken"></param>
        public static void ExtractWith7Z(
            string sourcePath,
            string targetDirectory,
            string password = null,
            CancellationTokenWrapper cancellationToken = null)
        {
            // https://superuser.com/questions/519114/how-to-write-error-status-for-command-line-7-zip-in-variable-or-instead-in-te

            ////0 = No error.
            ////1 = Warning(Non fatal error(s)).For example, one or more files were locked by some other application, so they were not compressed.
            ////2 = Fatal error.
            ////7 = Command line error.
            ////8 = Not enough memory for operation.
            ////255 = User stopped the process.

            var command = $"7z x {ConsoleCommand.EscapeForArgument(sourcePath)} -aoa -bsp1 -o{ConsoleCommand.EscapeForArgument(targetDirectory)} -y";

            if (password != null)
            {
                command += $" -p{ConsoleCommand.EscapeForArgument(password)}";
            }

            using (var console = new ConsoleCommand(cancellationToken: cancellationToken))
            {
                int exitCode = console.RunCommandAndWait(command, out string error);

                if (exitCode != 0)
                {
                    throw new InvalidDataException($"Error ({exitCode}) {error} extracting file: " + sourcePath);
                }
            }
        }

        /// <summary>
        /// Extract file using 7ZIP
        /// </summary>
        /// <param name="sourceFileOrDirectory"></param>
        /// <param name="targetFile"></param>
        /// <param name="compressionLevel"></param>
        /// <param name="pattern">When sourceFileOrDirectory is a directory, the pattern used in the 7z filter</param>
        /// <param name="password"></param>
        public static void CreateWith7Z(
            string sourceFileOrDirectory, 
            string targetFile, 
            int compressionLevel = 5, 
            string pattern = "*", 
            string password = null)
        {
            if (!Directory.Exists(sourceFileOrDirectory) && !File.Exists(sourceFileOrDirectory))
            {
                throw new Exception("Source directory does not exist.");
            }

            string directoryPatternPart = string.Empty;

            if (Directory.Exists(sourceFileOrDirectory))
            {
                directoryPatternPart = "\\" + pattern;
            }

            // https://superuser.com/questions/519114/how-to-write-error-status-for-command-line-7-zip-in-variable-or-instead-in-te

            ////0 = No error.
            ////1 = Warning(Non fatal error(s)).For example, one or more files were locked by some other application, so they were not compressed.
            ////2 = Fatal error.
            ////7 = Command line error.
            ////8 = Not enough memory for operation.
            ////255 = User stopped the process.

            var passwordOption = string.IsNullOrEmpty(password) ? string.Empty : $"-p{ConsoleCommand.EscapeForArgument(password)}";

            var command = $"7z a -mx{compressionLevel} {passwordOption} {ConsoleCommand.EscapeForArgument(targetFile)} {ConsoleCommand.EscapeForArgument(sourceFileOrDirectory + directoryPatternPart)} -bsp1";

            using (var console = new ConsoleCommand())
            {
                int exitCode = console.RunCommandAndWait(command, out string error);

                if (exitCode != 0)
                {
                    throw new InvalidDataException($"Error {error} compressing directory: " + sourceFileOrDirectory);
                }
            }
        }
    }
}
