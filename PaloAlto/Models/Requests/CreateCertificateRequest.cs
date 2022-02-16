using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Requests
{
    public class CreateCertificateRequest
    {
        [JsonProperty("hostnames")] public List<string> Hostnames { get; set; }
        [JsonProperty("requested_validity")] public int RequestedValidity { get; set; }
        [JsonProperty("request_type")] public string RequestType { get; set; }
        [JsonProperty("csr")] public string Csr { get; set; }
    }
}
