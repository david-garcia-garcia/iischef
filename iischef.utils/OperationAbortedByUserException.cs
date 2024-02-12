using System;
using System.Runtime.Serialization;

namespace iischef.utils
{
    /// <summary>
    /// The operation was aborted by the user or system operator
    /// </summary>
    [Serializable]
    public class OperationAbortedByUserException : Exception
    {
        public OperationAbortedByUserException() : base("Operation aborted")
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected static string GetAbortedAt()
        {
            string[] stackTraceLines = Environment.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (string line in stackTraceLines)
            {
                if (line.Contains("OperationAbortedByUser") || line.Contains("CancellationTokenWrapper"))
                {
                    continue;
                }

                if (line.Contains(":line"))
                {
                    return line;
                }
            }

            return null;
        }

        // Add this constructor for deserialization
        protected OperationAbortedByUserException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        // Override the GetObjectData method to include the AbortLocation during serialization
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            base.GetObjectData(info, context);
        }
    }
}
