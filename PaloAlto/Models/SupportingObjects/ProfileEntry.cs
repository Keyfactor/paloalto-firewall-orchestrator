using System;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    public class ProfileEntry
    {
        [XmlElement(ElementName = "protocol-settings")]
        public ProfileProtocolSettings ProtocolSettings { get; set; }

        [XmlElement(ElementName = "certificate")]
        public TlsCertificate Certificate { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "admin")]
        public string Admin { get; set; }

        [XmlAttribute(AttributeName = "dirtyId")]
        public int DirtyId { get; set; }

        [XmlIgnore]
        public DateTime Time { get; set; }

        [XmlElement("time")]
        public string DateTimeString
        {
            get => this.Time.ToString("yyyy-MM-dd HH:mm:ss");
            set => this.Time = DateTime.Parse(value);
        }

        [XmlText]
        public string Text { get; set; }
    }
}
