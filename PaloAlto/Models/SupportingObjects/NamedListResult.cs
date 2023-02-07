using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "result")]
    public class NamedListResult
    {

        [XmlElement(ElementName = "entry")]
        public List<NamedListEntry> Entry { get; set; }

        [XmlAttribute(AttributeName = "total-count")]
        public int TotalCount { get; set; }

        [XmlAttribute(AttributeName = "count")]
        public int Count { get; set; }
    }
}
