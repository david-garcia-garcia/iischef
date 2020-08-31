using System;
using System.Runtime.ExceptionServices;
using System.Transactions;

namespace iischef.core
{
    public static class ConsoleUtils
    {
        /// <summary>
        /// Get an application configured for usage through console (with a console logger and verbose mode)
        /// </summary>
        /// <returns></returns>
        public static Application GetApplicationForConsole(bool initialize = true)
        {
            var logger = new logger.ConsoleLogger();
            logger.SetVerbose(true);
            var app = new Application(logger);

            if (initialize)
            {
                app.Initialize();
                app.UseParentLogger();
            }

            return app;
        }

        /// <summary>
        /// Run the code and properly display unhandled exceptions in the console
        /// </summary>
        /// <param name="code"></param>
        /// <param name="memberName"></param>
        [NewRelic.Api.Agent.Transaction]
        public static void RunCode(
            Action code,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            NewRelic.Api.Agent.NewRelic.SetTransactionName("custom", "Console run: " + memberName);

            Exception hasException = null;

            try
            {
                code();
            }
            catch (Exception e)
            {
                var logger = new logger.ConsoleLogger();
                logger.LogException(e);
                hasException = e;
            }

            // Now throw
            if (hasException != null)
            {
                ExceptionDispatchInfo.Capture(hasException).Throw();
            }
        }
    }
}
