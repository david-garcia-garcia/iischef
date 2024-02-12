using System;
using System.Threading;
using Dynamitey;

namespace iischef.utils
{
    public class CancellationTokenWrapper
    {
        /// <summary>
        /// The actual token
        /// </summary>
        private CancellationToken Token;

        public bool IsCancellationRequested => this.Token.IsCancellationRequested;

        public CancellationToken GetToken => this.Token;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="token"></param>
        public CancellationTokenWrapper(CancellationToken token)
        {
            this.Token = token;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public CancellationTokenRegistration Register(Action callback)
        {
            return this.Token.Register(callback);
        }

        /// <summary>
        /// We are not using Token.ThrowIfCancellationRequested() because an exception
        /// of type System.OperationCancelled in a XUNIT test will terminate the test suite.
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            if (this.Token.IsCancellationRequested == true)
            {
                throw new OperationAbortedByUserException();
            }
        }
    }
}
