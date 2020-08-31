using System;

namespace iischef.core.Exceptions
{
    /// <summary>
    /// Exception that propagates upwards without being re-write to output at the application level
    /// </summary>
    public class AlreadyHandledException : Exception
    {
        public AlreadyHandledException(string message, Exception inner) : base(message, inner) 
        { 
        }
    }
}
