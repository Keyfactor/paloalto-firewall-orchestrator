using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto
{
    internal class JobProperties
    {

        public JobProperties()
        {
        }

        [JsonProperty("DeviceGroup")]
        [DefaultValue("")]
        public string DeviceGroup { get; set; }


    }
}
