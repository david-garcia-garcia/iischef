namespace iischef.core.Exceptions
{
    /// <summary>
    /// Use this exception type when you want to stop deployment, but this is not really considered a "failure".
    ///
    /// The error message is displayed at the application level.
    /// 
    /// </summary>
    public class StopDeploymentException : System.Exception
    {
        public StopDeploymentException(string message, System.Exception inner) : base(message, inner) 
        { 
        }

        public StopDeploymentException(string message) : base(message) 
        { 
        }
    }
}
