using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.utils.AppVeyor
{
    /// <summary>
    /// We need to keep track of job+build+buildVersion due to the
    /// limited capabilities of the AppVeyor API....
    /// </summary>
    public class Build
    {
        /// <summary>
        /// Get an instance of AppVeyorBuild
        /// </summary>
        /// <param name="serialized"></param>
        public Build()
        {
        }

        /// <summary>
        /// The job id
        /// </summary>
        public string buildId { get; set; }

        /// <summary>
        /// The jobs...
        /// </summary>
        public List<Job> jobs { get; set; }

        /// <summary>
        /// The build version
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// The build ID
        /// </summary>
        public int buildNumber { get; set; }

        /// <summary>
        /// The build message
        /// </summary>
        public string message { get; set; }

        /// <summary>
        /// The branch this builds belongs to.
        /// </summary>
        public string branch { get; set; }

        /// <summary>
        /// If this build is from a tag
        /// </summary>
        public bool isTag { get; set; }

        /// <summary>
        /// The commit id
        /// </summary>
        public string commitId { get; set; }

        /// <summary>
        /// The author name
        /// </summary>
        public string authorName { get; set; }

        /// <summary>
        /// The author username
        /// </summary>
        public string authorUsername { get; set; }

        /// <summary>
        /// The commiter name
        /// </summary>
        public string committerName { get; set; }

        /// <summary>
        /// The commiter username
        /// </summary>
        public string committerUsername { get; set; }

        /// <summary>
        /// The date this was commited at
        /// </summary>
        public DateTime committed { get; set; }

        /// <summary>
        /// The messages
        /// </summary>
        public List<string> messages { get; set; }

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

        /// <summary>
        /// The project
        /// </summary>
        public Project project { get; set; }
    }
}
