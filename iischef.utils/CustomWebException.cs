using System;

namespace iischef.utils
{
    /// <summary>
    /// Exception for web requests
    /// </summary>
    public class CustomWebException : Exception
    {
        public CustomWebException(string message, int statusCode, string contents) : base(message)
        {
            this.StatusCode = statusCode;
            this.Contents = contents;
        }

        public CustomWebException(string message, Exception innerException, int statusCode, string contents) : base(message, innerException)
        {
            this.StatusCode = statusCode;
            this.Contents = contents;
        }

        /// <summary>
        /// 
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Contents { get; set; }
    }
}
