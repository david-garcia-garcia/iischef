using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using iischef.logger;
using iischef.utils;

namespace iischef.core
{
    public static class HelperExtensions
    {
        /// <summary>
        /// Delete the artifact's source if it is remote
        /// </summary>
        /// <param name="artifact"></param>
        /// <param name="logger"></param>
        public static void DeleteIfRemote(this Artifact artifact, ILoggerInterface logger)
        {
            if (!artifact.isRemote)
            {
                return;
            }

            UtilsSystem.DeleteDirectory(artifact.localPath, logger);
        }
    }
}
