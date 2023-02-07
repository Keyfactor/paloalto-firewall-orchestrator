using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class NamedListResponse
    {

        [XmlElement(ElementName = "result")]
        public NamedListResult Result { get; set; }

        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }
    }
}
