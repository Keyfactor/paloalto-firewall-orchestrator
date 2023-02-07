using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "entry")]
    public class NamedListEntry
    {

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }
}
