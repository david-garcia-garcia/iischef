using System;
using System.Diagnostics;
using iischef.logger;
using Xunit.Abstractions;

namespace iischeftests
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class OutputHelperLogger : BaseLogger, ILoggerInterface
    {
        protected ITestOutputHelper Output;

        /// <summary>
        /// Start a filesystem based logger.
        /// </summary>
        public OutputHelperLogger(ITestOutputHelper output)
        {
            this.Output = output;
        }

        protected override void DoWrite(string content, EventLogEntryType type)
        {
            this.Output.WriteLine(DateTime.Now.ToLongTimeString() + "[" + type + "]" + content);
        }
    }
}
