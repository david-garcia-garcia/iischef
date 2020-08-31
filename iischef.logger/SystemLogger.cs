using System.Diagnostics;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class SystemLogger : BaseLogger, ILoggerInterface
    {
        protected EventLog log;

        /// <summary>
        /// Start a system based logger.
        /// </summary>
        /// <param name="name">Name for the service log group</param>
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
            this.log.WriteEntry(content, type);
        }
    }
}
