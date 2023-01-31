using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "result")]
	public class Result
	{

		[XmlElement(ElementName = "key")]
		public string Key { get; set; }
	}

	[XmlRoot(ElementName = "response")]
	public class AuthenticationResponse
	{

		[XmlElement(ElementName = "result")]
		public Result Result { get; set; }

		[XmlAttribute(AttributeName = "status")]
		public string Status { get; set; }

		[XmlText]
		public string Text { get; set; }
	}
}
