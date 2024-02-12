using iischef.core;
using iischef.logger;
using iischef.utils;
using System;
using System.Management.Automation;

namespace iischef.cmdlet
{
    public abstract class ChefCmdletBase : Cmdlet, IDisposable
    {
        private GlobalCancellationTokenManager cancellationManager;

        protected override void StopProcessing()
        {
            this.cancellationManager?.Cancel();
            base.StopProcessing();
        }

        protected override void BeginProcessing()
        {
            this.cancellationManager = new GlobalCancellationTokenManager(new ConsoleLogger());
            base.BeginProcessing();
        }

        protected override void EndProcessing()
        {
        }

        protected override void ProcessRecord()
        {
            var logger = ConsoleUtils.GetApplicationForConsole();
            
            ConsoleUtils.RunCode(logger, () =>
            {
                this.DoProcessRecord(logger);
            });

            base.ProcessRecord();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        protected abstract void DoProcessRecord(ILoggerInterface logger);

        /// <summary>
        /// Constructor
        /// </summary>
        public ChefCmdletBase()
        {
            ServicePointManagerExtensions.SetupServicePointManager();
        }

        public void Dispose()
        {
            this.cancellationManager?.Dispose();
        }
    }
}
