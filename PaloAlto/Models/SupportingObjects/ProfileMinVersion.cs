using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    public class ProfileMinVersion
    {
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
