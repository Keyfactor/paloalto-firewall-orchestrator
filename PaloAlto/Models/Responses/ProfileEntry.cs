using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "result")]
    public class ProfileResult
    {
        [XmlElement(ElementName = "entry")]
        public ProfileEntry Entry { get; set; }

        [XmlAttribute(AttributeName = "total-count")]
        public int TotalCount { get; set; }

        [XmlAttribute(AttributeName = "count")]
        public int Count { get; set; }

        [XmlText]
        public string Text { get; set; }
	}
}
