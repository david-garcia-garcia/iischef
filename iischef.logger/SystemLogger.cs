using System.Diagnostics;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class SystemLogger : BaseLogger, ILoggerInterface
    {
        // System logger is never verbose, we don't want to flood the event viewer
        protected override bool Verbose
        {
            get => false;
            set { }
        }

        /// <summary>
        /// 
        /// </summary>
        protected EventLog log;

        /// <summary>
        /// Start a system based logger.
        /// </summary>
        /// <param name="sLog">Name for the service log group</param>
        /// <param name="sSource"></param>
        public SystemLogger(string sLog, string sSource = null)
        {
            // None of these should have the SAME name
            // as the service otherwise you are screwed
            // when deploying.
            if (sSource == null)
            {
                sSource = sLog;
            }

            this.log = new EventLog(sLog);
            this.log.Source = sSource;

            if (!EventLog.SourceExists(sSource))
            {
                EventLog.CreateEventSource(sSource, sLog);
            }
        }

        protected override void DoWrite(string content, System.Diagnostics.EventLogEntryType type)
        {
            // Define the maximum size allowed for the content.
            const int MaxContentSize = 32766;

            // Check if the content exceeds the maximum size.
            if (content.Length > MaxContentSize)
            {
                // Truncate the content to fit within the maximum size, 
                // possibly adding an indicator at the end to show truncation.
                content = content.Substring(0, MaxContentSize - 100) + "...";
            }

            // Write the possibly truncated content to the event log, 
            // along with the specified entry type (e.g., Error, Information).
            this.log.WriteEntry(content, type);
        }
    }
}
