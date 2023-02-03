using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    public class ProfileListResult
    {
        [XmlElement(ElementName = "entry")]
        public List<ProfileEntry> Entry { get; set; }

        [XmlAttribute(AttributeName = "total-count")]
        public int TotalCount { get; set; }

        [XmlAttribute(AttributeName = "count")]
        public int Count { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
