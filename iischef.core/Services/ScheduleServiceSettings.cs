using System.Collections.Generic;

namespace iischef.core.Services
{
    public class ScheduleServiceSettings : DeployerSettingsBase
    {
        public string type { get; set; }

        public string id { get; set; }

        /// <summary>
        /// Use for a single command. Only here for BC reasons.
        /// </summary>
        public string command { get; set; }

        /// <summary>
        /// Use for multiple commands
        /// </summary>
        public List<string> commands { get; set; }

        /// <summary>
        /// Execution frequency, in minutes.
        /// </summary>
        public int frequency { get; set; }

        /// <summary>
        /// The user ID used for the task. Use "auto" to configure
        /// the application specific user.
        /// </summary>
        public string taskUserId { get; set; }

        /// <summary>
        /// The password for the user
        /// </summary>
        public string taskUserPassword { get; set; }

        /// <summary>
        /// The logon type for the task.
        /// </summary>
        public int? taskLogonType { get; set; }

        /// <summary>
        /// Set to true to install this as a disabled cron job
        /// </summary>
        public bool disabled { get; set; }
    }
}
