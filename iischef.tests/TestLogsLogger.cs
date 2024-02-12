using System;
using System.Diagnostics;
using System.IO;
using iischef.logger;

namespace iischeftests
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class TestLogsLogger : MemoryLogger
    {
        /// <summary>
        /// Target file to write to
        /// </summary>
        public string TargetFile { get; set; }

        /// <summary>
        /// Start a filesystem based logger.
        /// </summary>
        public TestLogsLogger(object owner, string test)
        {
            this.TargetFile = Path.Combine("c:\\testlogs", owner.GetType().Name, test + ".log");
            iischef.utils.UtilsSystem.EnsureDirectoryExists(this.TargetFile);

            if (File.Exists(this.TargetFile))
            {
                File.Delete(this.TargetFile);
            }

            this.SetVerbose(true);
        }

        protected override void DoWrite(string content, EventLogEntryType type)
        {
            File.AppendAllText(this.TargetFile, DateTime.Now.ToLongTimeString() + " [" + type + "] " + content + Environment.NewLine);
            base.DoWrite(content, type);
        }
    }
}
