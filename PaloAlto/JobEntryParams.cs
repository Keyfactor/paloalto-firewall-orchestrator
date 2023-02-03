using Newtonsoft.Json;
using System.ComponentModel;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto
{
    public class JobEntryParams
    {
        [JsonProperty("Trusted Root")]
        [DefaultValue(false)]
        public bool TrustedRoot { get; set; }

        [JsonProperty("TlsMinVersion")]
        [DefaultValue("")]
        public string TlsMinVersion { get; set; }

        [JsonProperty("TlsMaxVersion")]
        [DefaultValue("")]
        public string TlsMaxVersion { get; set; }

        [JsonProperty("TlsProfileName")]
        [DefaultValue("")]
        public string TlsProfileName { get; set; }
    }
}
