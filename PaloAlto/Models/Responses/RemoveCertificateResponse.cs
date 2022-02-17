using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class RemoveCertificateResponse
    {

        [XmlElement(ElementName = "msg")]
        public string Msg { get; set; }

        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
