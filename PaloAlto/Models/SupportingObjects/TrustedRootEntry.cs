using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "entry")]
    public class TrustedRootEntry
    {

        [XmlElement(ElementName = "filename")]
        public string Filename { get; set; }

        [XmlElement(ElementName = "subject")]
        public string Subject { get; set; }

        [XmlElement(ElementName = "common-name")]
        public string CommonName { get; set; }

        [XmlElement(ElementName = "issuer")]
        public string Issuer { get; set; }

        [XmlElement(ElementName = "serial-number")]
        public string SerialNumber { get; set; }

        [XmlElement(ElementName = "not-valid-after")]
        public string NotValidAfter { get; set; }

        [XmlElement(ElementName = "not-valid-before")]
        public string NotValidBefore { get; set; }

        [XmlElement(ElementName = "expiry-epoch")]
        public double ExpiryEpoch { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
