using iischef.logger;
using System;
using System.Threading;

namespace iischef.utils
{
    /// <summary>
    /// Bad design pattern, but useful if we want to avoid refactoring the whole thing.
    ///
    /// This application is NOT multithreaded, so we can statically handle a global cancellation
    /// token for the wrapping top level process (command, service, etc.) and consume it from within
    /// the stack.
    /// </summary>
    public class GlobalCancellationTokenManager : IDisposable
    {
        private CancellationTokenSource Source = new CancellationTokenSource();

        public GlobalCancellationTokenManager(ILoggerInterface logger = null)
        {
            if (Token != null)
            {
                throw new Exception("There is an existing cancellation token in the current scope");
            }

            Token = new CancellationTokenWrapper(this.Source.Token);
            this.Logger = logger;
        }

        /// <summary>
        /// 
        /// </summary>
        private static CancellationTokenWrapper Token;

        /// <summary>
        /// The cancellation token
        /// </summary>
        public static CancellationTokenWrapper CancellationToken => Token;

        /// <summary>
        /// 
        /// </summary>
        private ILoggerInterface Logger;

        public void Cancel()
        {
            this.Logger.LogWarning(false, "Cancellation requested by user...");
            this.Source.Cancel();
        }

        public void Dispose()
        {
            Token = null;
            this.Source.Dispose();
        }
    }
}
