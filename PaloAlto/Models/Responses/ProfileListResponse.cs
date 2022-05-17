﻿using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class ProfileListResponse
    {

        [XmlElement(ElementName = "result")]
        public ProfileListResult ProfileListResult { get; set; }

        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public int Code { get; set; }

        [XmlText]
        public string Text { get; set; }
	}
}
