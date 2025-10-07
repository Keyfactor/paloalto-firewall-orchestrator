using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class DeviceGroupsResponse
    {
        [XmlElement(ElementName = "result")]
        public DeviceGroupsResult Result { get; set; }
        
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }
        
        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }
    }

    public class DeviceGroupsResult
    {
        [XmlArray("device-group")]
        [XmlArrayItem("entry")]
        public List<DeviceGroup> DeviceGroups { get; set; }
        
        [XmlAttribute(AttributeName = "total-count")]
        public int TotalCount { get; set; }
        
        [XmlAttribute(AttributeName = "count")]
        public int Count { get; set; }
    }

    public class DeviceGroup
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlArray("reference-templates")]
        [XmlArrayItem("member")]
        public List<string> ReferenceTemplates { get; set; }

        [XmlArray("devices")]
        [XmlArrayItem("entry")]
        public List<DeviceEntry> Devices { get; set; }
    }
    
    public class DeviceEntry
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }
}

