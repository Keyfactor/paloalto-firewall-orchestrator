using System;
using System.Xml.Serialization;


namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "entry")]
    public class CertificateEntry
    {

        [XmlElement(ElementName = "subject-hash")]
        public SubjectHash SubjectHash { get; set; }

        [XmlElement(ElementName = "issuer-hash")]
        public IssuerHash IssuerHash { get; set; }

        [XmlElement(ElementName = "not-valid-before")]
        public NotValidBefore NotValidBefore { get; set; }

        [XmlElement(ElementName = "issuer")]
        public Issuer Issuer { get; set; }

        [XmlElement(ElementName = "not-valid-after")]
        public NotValidAfter NotValidAfter { get; set; }

        [XmlElement(ElementName = "common-name")]
        public CommonName CommonName { get; set; }

        [XmlElement(ElementName = "expiry-epoch")]
        public ExpiryEpoch ExpiryEpoch { get; set; }

        [XmlElement(ElementName = "ca")]
        public Ca Ca { get; set; }

        [XmlElement(ElementName = "subject")]
        public Subject Subject { get; set; }

        [XmlElement(ElementName = "public-key")]
        public PublicKey PublicKey { get; set; }

        [XmlElement(ElementName = "algorithm")]
        public Algorithm Algorithm { get; set; }

        [XmlElement(ElementName = "private-key")]
        public string PrivateKey { get; set; }

        [XmlElement(ElementName = "common-name-int")]
        public string CommonNameInt { get; set; }

        [XmlElement(ElementName = "subject-int")]
        public string SubjectInt { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "admin")]
        public string Admin { get; set; }

        [XmlAttribute(AttributeName = "dirtyId")]
        public int DirtyId { get; set; }

        [XmlIgnore]
        public DateTime Time { get; set; }

        [XmlElement("Time")]
        public string DateTimeString
        {
            get => this.Time.ToString("yyyy-MM-dd HH:mm:ss");
            set => this.Time = DateTime.Parse(value);
        }

        [XmlText]
        public string Text { get; set; }
    }
}
