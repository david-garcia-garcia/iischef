using System;
using System.Diagnostics;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class ConsoleLogger : BaseLogger, ILoggerInterface
    {
        /// <summary>
        /// Start a system based logger.
        /// </summary>
        public ConsoleLogger()
        {
        }

        /// <summary>
        /// If we are in a CI environment
        /// </summary>
        /// <returns></returns>
        protected bool RunningInContinuousIntegration()
        {
            return "True".Equals(Environment.GetEnvironmentVariable("CI"), StringComparison.CurrentCultureIgnoreCase);
        }

        protected override void DoWrite(string content, EventLogEntryType type)
        {
            if (content == null)
            {
                return;
            }

            // Do not mess with colors if we are in IC
            if (this.RunningInContinuousIntegration())
            {
                Console.Out.WriteLine(DateTime.Now.ToLongTimeString() + ": " + content.Replace("{", "{{").Replace("}", "}}"));
                return;
            }

            switch (type)
            {
                case EventLogEntryType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case EventLogEntryType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
            }

            Console.Out.WriteLine(DateTime.Now.ToLongTimeString() + ": " + content.Replace("{", "{{").Replace("}", "}}"));
            Console.ResetColor();
        }
    }
}
