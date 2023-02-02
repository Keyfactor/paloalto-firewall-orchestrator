using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "entry")]
    public class Entry
    {

        [XmlElement(ElementName = "subject-hash")]
        public string SubjectHash { get; set; }

        [XmlElement(ElementName = "issuer-hash")]
        public string IssuerHash { get; set; }

        [XmlElement(ElementName = "not-valid-before")]
        public string NotValidBefore { get; set; }

        [XmlElement(ElementName = "issuer")]
        public string Issuer { get; set; }

        [XmlElement(ElementName = "not-valid-after")]
        public string NotValidAfter { get; set; }

        [XmlElement(ElementName = "common-name")]
        public string CommonName { get; set; }

        [XmlElement(ElementName = "expiry-epoch")]
        public int ExpiryEpoch { get; set; }

        [XmlElement(ElementName = "ca")]
        public string Ca { get; set; }

        [XmlElement(ElementName = "subject")]
        public string Subject { get; set; }

        [XmlElement(ElementName = "public-key")]
        public string PublicKey { get; set; }

        [XmlElement(ElementName = "algorithm")]
        public string Algorithm { get; set; }

        [XmlElement(ElementName = "private-key")]
        public string PrivateKey { get; set; }

        [XmlElement(ElementName = "common-name-int")]
        public string CommonNameInt { get; set; }

        [XmlElement(ElementName = "subject-int")]
        public string SubjectInt { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
