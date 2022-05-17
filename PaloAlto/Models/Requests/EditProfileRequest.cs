using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Requests
{
    public class EditProfileRequest
    {
        [XmlElement(ElementName = "protocol-settings")]
        public ProfileProtocolSettings ProtocolSettings { get; set; }

        [XmlElement(ElementName = "certificate")]
        public string Certificate { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlText]
        public string Text { get; set; }
	}
}
