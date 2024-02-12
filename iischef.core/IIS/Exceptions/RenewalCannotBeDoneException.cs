using iischef.utils;
using System;

namespace iischef.core.IIS.Exceptions
{
    public class RenewalCannotBeDoneException : BusinessRuleException
    {
        public RenewalCannotBeDoneException(string message) : base(message)
        {
        }
    }
}
