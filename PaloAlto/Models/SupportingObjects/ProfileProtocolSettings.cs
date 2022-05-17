using System;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    public class ProfileProtocolSettings
    {
        [XmlElement(ElementName = "min-version")]
        public ProfileMinVersion MinVersion { get; set; }

        [XmlElement(ElementName = "max-version")]
        public ProfileMaxVersion MaxVersion { get; set; }

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
