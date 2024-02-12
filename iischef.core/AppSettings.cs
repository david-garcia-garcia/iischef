using Newtonsoft.Json;

namespace iischef.core
{
    /// <summary>
    /// 
    /// </summary>
    public class AppSettings
    {
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
