using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "trusted-root-ca")]
    public class TrustedRootCa
    {

        [XmlElement(ElementName = "entry")]
        public List<TrustedRootEntry> Entry { get; set; }
    }
}
