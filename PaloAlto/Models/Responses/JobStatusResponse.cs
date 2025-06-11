using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class JobStatusResponse
    {
        [XmlElement(ElementName = "result")]
        public JobStatusResult Result { get; set; }
    }

    public class JobStatusResult
    {
        [XmlElement(ElementName = "job")]
        public Job Job { get; set; }
    }

    public class Job
    {
        [XmlElement(ElementName = "progress")]
        public string Progress { get; set; }
        
        [XmlElement(ElementName = "result")]
        public string Result { get; set; }
        
        [XmlElement(ElementName = "status")]
        public string Status { get; set; }
        
        [XmlElement(ElementName = "details")]
        public Msg Details { get; set; }
    }
}

