namespace iischef.core.Exceptions
{
    /// <summary>
    /// Transient error, will not mark the deployment as failed, and will deployments won't block
    /// </summary>
    internal class TransientErrorException : System.Exception
    {
        public TransientErrorException(string message, System.Exception inner) : base(message, inner) 
        { 
        }

        public TransientErrorException(string message) : base(message) 
        { 
        }
    }
}
