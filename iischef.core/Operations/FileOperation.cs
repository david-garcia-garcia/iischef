using System;

namespace iischef.core.Operations
{
    public class FileOperation
    {
        /// <summary>
        /// Source file or directory
        /// </summary>
        public string source { get; set; }

        /// <summary>
        /// Destination file or directory
        /// </summary>
        public string destination { get; set; }

        /// <summary>
        /// Operation type
        /// </summary>
        public string action { get; set; }

        /// <summary>
        /// This copy operations are restricted to the path
        /// </summary>
        /// <param name="path"></param>
        public void execute(string path)
        {
            var sourcepath = path + "\\" + this.source;
            var destinationpath = path + "\\" + this.destination;

            switch (this.action)
            {
                case "move":
                    if (System.IO.File.Exists(sourcepath))
                    {
                        iischef.utils.UtilsSystem.EnsureDirectoryExists(destinationpath);
                        System.IO.File.Copy(sourcepath, destinationpath);
                        System.IO.File.Delete(sourcepath);
                    }

                    break;
                case "copy":
                    if (System.IO.File.Exists(sourcepath))
                    {
                        iischef.utils.UtilsSystem.EnsureDirectoryExists(destinationpath);
                        System.IO.File.Copy(sourcepath, destinationpath);
                    }

                    break;
                default:
                    throw new Exception("File operation type {0} not supported.");
            }
        }
    }
}
