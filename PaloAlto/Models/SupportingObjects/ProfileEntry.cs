using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    public class ProfileEntry
    {
        [XmlElement(ElementName = "protocol-settings")]
        public ProfileProtocolSettings ProtocolSettings { get; set; }

        [XmlElement(ElementName = "certificate")]
        public Certificate Certificate { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "admin")]
        public string Admin { get; set; }

        [XmlAttribute(AttributeName = "dirtyId")]
        public int DirtyId { get; set; }

        [XmlAttribute(AttributeName = "time")]
        public string Time { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
