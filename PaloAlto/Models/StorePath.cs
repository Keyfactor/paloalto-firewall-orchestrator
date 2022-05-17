using System.ComponentModel;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models
{
    internal class StorePath
    {
        [JsonProperty("ProtocolMinVersion")]
        public string ProtocolMinVersion { get; set; }

        [JsonProperty("ProtocolMaxVersion")]
        public string ProtocolMaxVersion { get; set; }
    }
}