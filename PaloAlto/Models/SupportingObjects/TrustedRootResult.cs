using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "result")]
    public class TrustedRootResult
    {

        [XmlElement(ElementName = "trusted-root-ca")]
        public TrustedRootCa TrustedRootCa { get; set; }

        [XmlAttribute(AttributeName = "total-count")]
        public int TotalCount { get; set; }

        [XmlAttribute(AttributeName = "count")]
        public int Count { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
