using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
	[XmlRoot(ElementName = "certificate")]
	public class TlsCertificate
	{

		[XmlAttribute(AttributeName = "admin")]
		public string Admin { get; set; }

		[XmlAttribute(AttributeName = "dirtyId")]
		public int DirtyId { get; set; }

		[XmlIgnore]
		public DateTime Time { get; set; }

		[XmlElement("time")]
		public string DateTimeString
		{
			get => this.Time.ToString("yyyy-MM-dd HH:mm:ss");
			set => this.Time = DateTime.Parse(value);
		}

		[XmlText]
		public string Text { get; set; }
	}
}
