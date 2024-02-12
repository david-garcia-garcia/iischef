using System;
using System.Threading;

namespace iischef.logger
{
    /// <summary>
    /// Logger (system log) for the application.
    /// </summary>
    public class ProgressLogger : IDisposable
    {
        private const int TimerPeriod = 750;
        private ILoggerInterface logger;
        private volatile string LastProgress;
        private int? LastWrittenLength;
        private Timer LazyWriterTimer;
        private object Lock = new object();

        public void StartProgress()
        {
            try
            {
                var consoleAvailable = Console.WindowHeight > 0;
                this.LazyWriterTimer?.Change(TimerPeriod, Timeout.Infinite);
            }
            catch
            {
                // Do nothing
            }
        }

        /// <summary>
        /// Clears the current progress from the console, and writes to the real
        /// logger the last progress information
        /// </summary>
        public void ProgressEndAndPersist()
        {
            lock (this.Lock)
            {
                this.LazyWriterTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }

            this.DoWriteToConsole(string.Empty);
            this.logger.LogInfo(false, this.LastProgress);
            this.LastWrittenLength = null;
            this.LastProgress = null;
            this.StartProgress();
        }

        /// <summary>
        /// Start a system based logger.
        /// </summary>
        public ProgressLogger(ILoggerInterface logger)
        {
            this.logger = logger;

            lock (this.Lock)
            {
                this.LazyWriterTimer = new Timer(this.WriteToConsole, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void DoWrite(string message)
        {
            this.LastProgress = message;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ResetWrittenLength()
        {
            this.LastWrittenLength = 0;
        }

        private void WriteToConsole(object state)
        {
            if (this.LastProgress == null)
            {
                this.LazyWriterTimer?.Change(TimerPeriod, Timeout.Infinite);
                return;
            }

            // Borramos lo último que se hubiera escrito
            this.DoWriteToConsole(this.LastProgress);
        }

        private void DoWriteToConsole(string literal)
        {
            lock (this.Lock)
            {
                if (literal == null)
                {
                    return;
                }

                // Borramos lo último que se hubiera escrito
                if (this.LastWrittenLength != null)
                {
                    Console.Write("\r" + new string((char)32, this.LastWrittenLength.Value) + "\r");
                }

                Console.Write(literal);

                this.LastWrittenLength = literal.Length;
                this.LazyWriterTimer?.Change(TimerPeriod, Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            this.LazyWriterTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
