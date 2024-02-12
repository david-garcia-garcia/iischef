using System;
using System.Diagnostics;

namespace iischef.logger
{
    public interface ILoggerInterface
    {
        void SetVerbose(bool verbose);

        void LogError(string message, params object[] replacements);

        void LogInfo(bool verbose, string message, params object[] replacements);

        void LogWarning(bool verbose, string message, params object[] replacements);

        void LogException(Exception e, EventLogEntryType entryType = EventLogEntryType.Error);
    }
}
