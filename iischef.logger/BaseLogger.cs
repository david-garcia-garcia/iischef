using iischef.utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace iischef.logger
{
    public abstract class BaseLogger : ILoggerInterface
    {
        protected virtual bool Verbose { get; set; }

        /// <summary>
        /// Enabel or disable verbose mode for the logger.
        /// </summary>
        /// <param name="verbose"></param>
        public void SetVerbose(bool verbose)
        {
            this.Verbose = verbose;
        }

        protected void WriteEntry(bool verbose, string content, object[] replacements, EventLogEntryType type)
        {
            if (verbose && !this.Verbose)
            {
                return;
            }

            string data = content;

            try
            {
                data = string.Format(content, replacements);
                data = data
                    .TrimEnd(Environment.NewLine.ToCharArray())
                    .TrimStart(Environment.NewLine.ToCharArray());
            }
            catch
            {
                // ignored
            }

            this.DoWrite(data, type);
        }

        protected abstract void DoWrite(string content, EventLogEntryType type);

        /// <summary>
        /// Everything we log in this monitor application
        /// is an error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="replacements"></param>
        public void LogError(string message, params object[] replacements)
        {
            this.WriteEntry(false, message, replacements, EventLogEntryType.Error);
        }

        public void LogInfo(bool verbose, string message, params object[] replacements)
        {
            this.WriteEntry(verbose, message, replacements, EventLogEntryType.Information);
        }

        public void LogWarning(bool verbose, string message, params object[] replacements)
        {
            this.WriteEntry(verbose, message, replacements, EventLogEntryType.Warning);
        }

        public void LogException(Exception e, EventLogEntryType entryType = EventLogEntryType.Error)
        {
            if (e is BusinessRuleException)
            {
                this.WriteEntry(false, e.Message, null, EventLogEntryType.Error);
                return;
            }

            StringBuilder sb = new StringBuilder();
            this.DumpException(e, sb);
            this.WriteEntry(false, sb.ToString(), null, entryType);
        }

        /// <summary>
        /// Make sure that relevant details are extract from exceptions!
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sb"></param>
        /// <param name="depth"></param>
        protected void DumpException(Exception e, StringBuilder sb, int depth = 0)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            string prefix = string.Concat(Enumerable.Repeat("  ", depth));

            var errorCode = ErrorUtils.GetExceptionErrorCode(e);

            sb.AppendLine(prefix + $"{e.GetType().FullName} (uint)e.HResult=[{(uint)e.HResult}] ErrorCode={errorCode}");
            sb.AppendLine(prefix + (string.IsNullOrWhiteSpace(e.Message) ? "No message" : e.Message));
            string cleanTrace = ErrorUtils.CleanStackTrace(e.StackTrace);
            sb.AppendLine(prefix + (string.IsNullOrWhiteSpace(cleanTrace) ? "No stack trace" : cleanTrace));

            if (e is ReflectionTypeLoadException reflectionTypeLoadException)
            {
                foreach (var loaderException in reflectionTypeLoadException.LoaderExceptions)
                {
                    sb.AppendLine(prefix + "Loader exception:" + loaderException.Message);

                    if (loaderException.InnerException != null)
                    {
                        this.DumpException(loaderException.InnerException, sb, depth + 1);
                    }
                }
            }

            if (e is BadImageFormatException badImageFormatException)
            {
                sb.AppendLine(prefix + "FileName: " + badImageFormatException.FileName);
                sb.AppendLine(prefix + "FusionLog: " + badImageFormatException.FusionLog);
                sb.AppendLine(prefix + "Environment.Is64BitProcess: " + (Environment.Is64BitProcess ? "yes" : "no"));
            }

            if (e is FileNotFoundException fileNotFoundException)
            {
                sb.AppendLine(prefix + "FusionLog: " + fileNotFoundException.FusionLog);
                sb.AppendLine(prefix + "FileName: " + fileNotFoundException.FileName);
            }

            if (e is AggregateException aggregateException)
            {
                foreach (var innerAggregate in aggregateException.InnerExceptions)
                {
                    sb.AppendLine(prefix + "Aggregate Exception");
                    this.DumpException(innerAggregate, sb, depth + 1);
                }
            }

            if (e.InnerException != null)
            {
                sb.AppendLine(prefix + "Inner Exception");
                this.DumpException(e.InnerException, sb, depth + 1);
            }
        }
    }
}
