using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, DateTime?> FeatureEnabledLastSuccess { get; set; }

        public string PfxPasswordEncoded { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public string PfxPassword
        {
            get => this.PfxPasswordEncoded == null ? null : DPapiStore.Decode(this.PfxPasswordEncoded);
            set => this.PfxPasswordEncoded = value == null ? null : DPapiStore.Encode(value);
        }

        public string CcsAccountPasswordEncoded { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public string CcsAccountPassword
        {
            get => this.CcsAccountPasswordEncoded == null ? null : DPapiStore.Decode(this.CcsAccountPasswordEncoded);
            set => this.CcsAccountPasswordEncoded = value == null ? null : DPapiStore.Encode(value);
        }
    }
}
