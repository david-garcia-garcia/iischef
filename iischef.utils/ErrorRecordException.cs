using System;
using System.Management.Automation;

namespace iischef.utils
{
    public class ErrorRecordException : Exception
    {
        public ErrorRecord ErrorRecord { get; private set; }

        public ErrorRecordException(ErrorRecord record) : base(record.Exception.Message)
        {
            this.ErrorRecord = record;
        }
    }
}
