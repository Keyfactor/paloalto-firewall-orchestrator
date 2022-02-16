﻿using System;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "expiry-epoch")]
    public class ExpiryEpoch
    {
        [XmlAttribute(AttributeName = "admin")]
        public string Admin { get; set; }

        [XmlAttribute(AttributeName = "dirtyId")]
        public int DirtyId { get; set; }

        [XmlIgnore]
        public DateTime Time { get; set; }

        [XmlElement("Time")]
        public string DateTimeString
        {
            get => this.Time.ToString("yyyy-MM-dd HH:mm:ss");
            set => this.Time = DateTime.Parse(value);
        }

        public double Text { get; set; }
	}
}
