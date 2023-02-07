using System.ComponentModel;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto
{
    public class JobProperties
    {
        [JsonProperty("DeviceGroup")]
        [DefaultValue("")]
        public string DeviceGroup { get; set; }


    }
}
