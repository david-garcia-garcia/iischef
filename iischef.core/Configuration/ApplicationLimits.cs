using Microsoft.Web.Administration;

namespace iischef.core.Configuration
{
    /// <summary>
    /// These are specific configuration limits to be configured
    /// on the server itself, that will have an effect/limit on the different
    /// deployer settings. These settings are related to application stability
    /// and resource contention.
    /// </summary>
    public class ApplicationLimits
    {
        /// <summary>
        /// Populate limit defaults if they are missing
        /// </summary>
        public static void PopulateDefaultsIfMissing(ApplicationLimits limits)
        {
            // Max CGI processes for an application
            if (limits.FastCgiMaxInstances == null || limits.FastCgiMaxInstances == 0)
            {
                limits.FastCgiMaxInstances = 10;
            }

            // 3GB default is more than enough, consider that x86 is limited to about 2GB (real)
            if (limits.IisPoolMaxPrivateMemoryLimitKb == null || limits.IisPoolMaxPrivateMemoryLimitKb == 0)
            {
                limits.IisPoolMaxPrivateMemoryLimitKb = (int)3.5 * 1024 * 1024;
            }

            // Throttle under load
            if (string.IsNullOrWhiteSpace(limits.IisPoolCpuLimitAction))
            {
                limits.IisPoolCpuLimitAction = ProcessorAction.ThrottleUnderLoad.ToString();
            }

            if (limits.IisPoolMaxCpuLimitPercent == null || limits.IisPoolMaxCpuLimitPercent == 0)
            {
                limits.IisPoolMaxCpuLimitPercent = 50;
            }

            // Startup mode
            if (limits.IisPoolStartupModeAllowAlwaysRunning == null)
            {
                limits.IisPoolStartupModeAllowAlwaysRunning = true;
            }

            if (limits.IisVirtualDirectoryAllowPreloadEnabled == null)
            {
                limits.IisVirtualDirectoryAllowPreloadEnabled = true;
            }

            if (string.IsNullOrWhiteSpace(limits.IisPoolIdleTimeoutAction))
            {
                limits.IisPoolIdleTimeoutAction = Microsoft.Web.Administration.IdleTimeoutAction.Suspend.ToString();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int? FastCgiMaxInstances { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public long? IisPoolMaxCpuLimitPercent { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string IisPoolCpuLimitAction { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int? IisPoolMaxPrivateMemoryLimitKb { get; set; }

        /// <summary>
        /// Pool startup mode + PreloadEnabled can be disabled for high density environments
        /// where we do not want sites to be fully loaded and sitting idle
        /// </summary>
        public bool? IisPoolStartupModeAllowAlwaysRunning { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool? IisVirtualDirectoryAllowPreloadEnabled { get; set; }

        /// <summary>
        /// Action to take when pool is idle
        /// </summary>
        public string IisPoolIdleTimeoutAction { get; set; }
    }
}
