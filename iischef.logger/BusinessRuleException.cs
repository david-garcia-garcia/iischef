using System;

namespace iischef.utils
{
    /// <summary>
    /// Exception that propagates, but only the message is logged (no stack trace or internal details)
    /// </summary>
    [Serializable]
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public BusinessRuleException(string message) : base(message, null) 
        { 
        }
    }
}
