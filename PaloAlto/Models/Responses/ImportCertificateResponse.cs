using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{

        [XmlRoot(ElementName = "response")]
        public class ImportCertificateResponse
        {

            [XmlElement(ElementName = "result")]
            public string Result { get; set; }

            [XmlAttribute(AttributeName = "status")]
            public string Status { get; set; }

            [XmlText]
            public string Text { get; set; }
        }
}
