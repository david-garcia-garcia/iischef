using iischef.logger;
using iischef.utils;
using System;
using System.Management.Automation;
using System.Runtime.ExceptionServices;

namespace iischef.core
{
    public static class ConsoleUtils
    {
        /// <summary>
        /// Get an application configured for usage through console (with a console logger and verbose mode)
        /// </summary>
        /// <returns></returns>
        public static ILoggerInterface GetApplicationForConsole()
        {
            var result = new ChainedLogger();
            result.AddTarget(new logger.ConsoleLogger());
            result.AddTarget(new SystemLogger("application", "chef"));
            return result;
        }

        /// <summary>
        /// Run the code and properly display unhandled exceptions in the console
        /// </summary>
        /// <param name="code"></param>
        /// <param name="logger"></param>
        public static void RunCode(
            ILoggerInterface logger,
            Action code)
        {
            logger = logger ?? new ConsoleLogger();

            try
            {
                code();
            }
            catch (BusinessRuleException businessRuleException)
            {
                logger.LogError(businessRuleException.Message);
                ExceptionDispatchInfo.Capture(businessRuleException).Throw();
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is PipelineStoppedException || ex is OperationAbortedByUserException)
            {
                DumpToConsole(ex);
                logger.LogException(ex);
            }
            catch (Exception e)
            {
                DumpToConsole(e);
                logger.LogException(e);
            }
        }

        /// <summary>
        /// Dump exception details directly into the console, helpful for Continuous Integration Output
        /// </summary>
        private static void DumpToConsole(Exception e, int depth = 0)
        {
            if (depth > 5)
            {
                return;
            }

            if (e == null)
            {
                return;
            }

            (new ConsoleLogger()).LogError(e.Message + Environment.NewLine + e.StackTrace);

            if (e is AggregateException aggregateException)
            {
                foreach (var aggregate in aggregateException.InnerExceptions)
                {
                    DumpToConsole(aggregate, depth + 1);
                }
            }

            DumpToConsole(e.InnerException, depth + 1);
        }
    }
}
