namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class NullLogger : BaseLogger, ILoggerInterface
    {
        public NullLogger()
        {
        }

        protected override void DoWrite(string content, System.Diagnostics.EventLogEntryType type)
        {
        }
    }
}
