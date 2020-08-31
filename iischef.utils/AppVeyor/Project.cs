using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.utils.AppVeyor
{
    /// <summary>
    /// AppVeyor project DTO
    /// </summary>
    public class Project
    {
        /// <summary>
        /// The project id
        /// </summary>
        public long projectId { get; set; }

        /// <summary>
        /// The account id
        /// </summary>
        public long accountId { get; set; }

        /// <summary>
        /// The account name
        /// </summary>
        public string accountName { get; set; }

        /// <summary>
        /// The project name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// The repository slug
        /// </summary>
        public string slug { get; set; }

        /// <summary>
        /// The repository type
        /// </summary>
        public string repositoryType { get; set; }

        /// <summary>
        /// The repository name
        /// </summary>
        public string repositoryName { get; set; }

        /// <summary>
        /// The repository branch.
        /// </summary>
        public string repositoryBranch { get; set; }
    }
}
