namespace iischef.core.IIS
{
    public class Pool
    {
        /// <summary>
        /// The id for the pool
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Enable 32 Bit processes
        /// </summary>
        public bool Enable32BitAppOnWin64 { get; set; } = true;

        /// <summary>
        /// Autostart
        /// </summary>
        public bool AutoStart { get; set; } = false;

        public string ManagedPipelineMode { get; set; }

        public string StartMode { get; set; }

        public bool LoadUserProfile { get; set; }

        public string ManagedRuntimeVersion { get; set; }

        /// <summary>
        /// Supports:
        /// - ChefApp
        /// - ApplicationPoolIdentity
        /// - LocalService
        /// - LocalSystem
        /// - NetworkService
        /// - Superuser (a super privileged local account)
        /// </summary>
        public string IdentityType { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public long CpuLimitPercent { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string CpuLimitAction { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int PrivateMemoryLimitKb { get; set; }
    }
}
