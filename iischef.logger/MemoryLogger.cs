using System;
using System.Diagnostics;
using System.IO;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class MemoryLogger : BaseLogger, ILoggerInterface
    {
        protected StringWriter Sw;

        public string GetLog()
        {
            return this.Sw.ToString();
        }

        /// <summary>
        /// Start a filesystem based logger.
        /// </summary>
        public MemoryLogger()
        {
            this.Sw = new StringWriter();
        }

        public void Clear()
        {
            this.Sw = new StringWriter();
        }

        protected override void DoWrite(string content, EventLogEntryType type)
        {
            this.Sw.WriteLine(DateTime.Now.ToLongTimeString() + "[" + type + "]" + content);
        }
    }
}
