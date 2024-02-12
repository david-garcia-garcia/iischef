using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class FileLogger : BaseLogger, IDisposable
    {
        /// <summary>
        /// The logger
        /// </summary>
        protected ILogger Logger;

        /// <summary>
        /// The log factory
        /// </summary>
        protected LogFactory LogFactory;

        /// <summary> 
        /// Start a filesystem based logger.
        /// </summary>
        /// <param name="path">File name for the log file.</param>
        public FileLogger(string path)
        {
            this.LogFactory = new LogFactory();

            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget("File");
            fileTarget.FileName = path;

            fileTarget.KeepFileOpen = false;

            fileTarget.ArchiveAboveSize = (1024 * 1024) * 5;

            config.AddTarget("file", fileTarget);

            var rule1 = new LoggingRule("*", LogLevel.Trace, fileTarget);
            config.LoggingRules.Add(rule1);

            this.LogFactory.Configuration = config;

            string dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            this.Logger = this.LogFactory.GetLogger("iischef");
        }

        /// <summary>
        /// Write to the logger
        /// </summary>
        /// <param name="content"></param>
        /// <param name="type"></param>
        protected override void DoWrite(string content, EventLogEntryType type)
        {
            switch (type)
            {
                case EventLogEntryType.Error:
                    this.Logger.Error(content);
                    break;
                case EventLogEntryType.Warning:
                    this.Logger.Warn(content);
                    break;
                default:
                    this.Logger.Trace(content);
                    break;
            }
        }

        // Dispose the log factory
        public void Dispose()
        {
            this.LogFactory?.Dispose();
            this.LogFactory = null;
        }

        // Just in case the disposable pattern is not properly implemented
        ~FileLogger()
        {
            this.Dispose();
        }
    }
}
