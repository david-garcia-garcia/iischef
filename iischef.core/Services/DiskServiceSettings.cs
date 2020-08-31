using System.Collections.Generic;

namespace iischef.core.Services
{
    public class DiskServiceSettings : DeployerSettingsBase
    {
        public string type { get; set; }

        public string id { get; set; }

        public Dictionary<string, Mount> mounts { get; set; }
    }
}
