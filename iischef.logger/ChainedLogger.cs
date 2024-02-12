using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iischef.logger
{
    /// <summary>
    /// Logger para enlazar varios loggers
    /// </summary>
    public class ChainedLogger : ILoggerInterface
    {
        /// <summary>
        /// 
        /// </summary>
        private List<ILoggerInterface> Loggers = new List<ILoggerInterface>();

        /// <summary>
        /// 
        /// </summary>
        public ChainedLogger()
        {
        }

        public void SetVerbose(bool verbose)
        {
            foreach (var l in this.Loggers)
            {
                l.SetVerbose(verbose);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerInterface"></param>
        public void AddTarget(ILoggerInterface loggerInterface)
        {
            this.Loggers.Add(loggerInterface);
        }

        public void LogError(string message, params object[] replacements)
        {
            foreach (var loggerInterface in this.Loggers)
            {
                loggerInterface?.LogError(message, replacements);
            }
        }

        public void LogInfo(bool verbose, string message, params object[] replacements)
        {
            foreach (var loggerInterface in this.Loggers)
            {
                loggerInterface?.LogInfo(verbose, message, replacements);
            }
        }

        public void LogWarning(bool verbose, string message, params object[] replacements)
        {
            foreach (var loggerInterface in this.Loggers)
            {
                loggerInterface?.LogWarning(verbose, message, replacements);
            }
        }

        public void LogException(Exception e, EventLogEntryType entryType = EventLogEntryType.Error)
        {
            foreach (var loggerInterface in this.Loggers)
            {
                loggerInterface?.LogException(e, entryType);
            }
        }
    }
}
