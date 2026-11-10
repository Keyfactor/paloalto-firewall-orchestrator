using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;

[XmlRoot(ElementName = "response")]
public class ManagementInterfaceServiceProfileResponse
{
    [XmlElement(ElementName = "result")]
    public ManagementInterfaceServiceProfileResult Result { get; set; }

    [XmlAttribute(AttributeName = "status")]
    public string Status { get; set; }

    [XmlAttribute(AttributeName = "code")]
    public int Code { get; set; }
}

public class ManagementInterfaceServiceProfileResult
{
    [XmlElement(ElementName = "ssl-tls-service-profile")]
    public string SslTlsServiceProfile { get; set; }

    [XmlAttribute(AttributeName = "count")]
    public int Count { get; set; }

    [XmlAttribute(AttributeName = "total-count")]
    public int TotalCount { get; set; }
}