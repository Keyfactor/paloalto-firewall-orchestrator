using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class GetProfileByCertificateResponse
    {
        [XmlElement(ElementName = "result")]
        public ProfileResult Result { get; set; }

        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }

        [XmlText]
        public string Text { get; set; }
	}
}
