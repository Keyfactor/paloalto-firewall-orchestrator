using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class TemplateStacksResponse
    {
        [XmlElement(ElementName = "result")]
        public TemplateStackResult Result { get; set; }
        
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }
        
        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }
    }

    public class TemplateStackResult
    {
        [XmlArray("template-stack")]
        [XmlArrayItem("entry")]
        public List<TemplateStack> TemplateStacks { get; set; }
    }

    public class TemplateStack
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        
        [XmlArray("templates")]
        [XmlArrayItem("member")]
        public List<string> Templates { get; set; }
    }
}


