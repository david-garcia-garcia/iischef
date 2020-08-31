using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.utils.AppVeyor
{
    /// <summary>
    /// An appveyor build job
    /// </summary>
    public class Job
    {
        /// <summary>
        /// The jobId
        /// </summary>
        public string jobId { get; set; }

        /// <summary>
        /// Job name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Operating system type
        /// </summary>
        public string osType { get; set; }

        /// <summary>
        /// Allow failures
        /// </summary>
        public bool allowFailure { get; set; }

        public int messagesCount { get; set; }

        public int compilationMessagesCount { get; set; }

        public int compilationErrorsCount { get; set; }

        public int compilationWarningsCount { get; set; }

        /// <summary>
        /// Number of tests
        /// </summary>
        public int testsCount { get; set; }

        /// <summary>
        /// Number of passed tests
        /// </summary>
        public int passedTestsCount { get; set; }

        /// <summary>
        /// Number of failed tests
        /// </summary>
        public int failedTestsCount { get; set; }

        /// <summary>
        /// Number of artifacts
        /// </summary>
        public int artifactsCount { get; set; }

        /// <summary>
        /// The status
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// When started
        /// </summary>
        public DateTime started { get; set; }

        /// <summary>
        /// When finished
        /// </summary>
        public DateTime finished { get; set; }

        /// <summary>
        /// When created
        /// </summary>
        public DateTime created { get; set; }

        /// <summary>
        /// When updated
        /// </summary>
        public DateTime updated { get; set; }
    }
}
