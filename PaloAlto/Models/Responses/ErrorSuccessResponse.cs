using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "msg")]
    public class Msg
    {

        [XmlElement(ElementName = "line")]
        public List<string> Line { get; set; }

        [XmlText]
        public string StringMsg { get; set; }
    }

    [XmlRoot(ElementName = "response")]
    public class ErrorSuccessResponse
    {

        [XmlElement(ElementName = "msg",IsNullable = true)]
        public Msg LineMsg { get; set; }

        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
