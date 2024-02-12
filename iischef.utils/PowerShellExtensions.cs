using iischef.logger;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace iischef.utils
{
    public static class PowerShellExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ps"></param>
        /// <param name="logger"></param>
        public static Collection<PSObject>
            InvokeAndTreatError(this PowerShell ps, ILoggerInterface logger)
        {
            if (ps == null)
            {
                throw new ArgumentNullException(nameof(ps));
            }

            var result = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = ps.Streams.Error.ReadAll();

                if (errors.Count == 0)
                {
                    // Caso de que haya un SilentlyContinue, no registra el error y no deberiá lanzar error.
                    logger.LogWarning(false, "Possible error while running command at " + Environment.NewLine + Environment.StackTrace);
                    return result;
                }

                if (errors.Count == 1)
                {
                    throw new ErrorRecordException(errors.First());
                }

                throw new AggregateException(errors.Select((i) => new ErrorRecordException(i)));
            }

            return result;
        }
    }
}
