// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects
{
    [XmlRoot(ElementName = "entry")]
    public class TrustedRootEntry
    {

        [XmlElement(ElementName = "filename")]
        public string Filename { get; set; }

        [XmlElement(ElementName = "subject")]
        public string Subject { get; set; }

        [XmlElement(ElementName = "common-name")]
        public string CommonName { get; set; }

        [XmlElement(ElementName = "issuer")]
        public string Issuer { get; set; }

        [XmlElement(ElementName = "serial-number")]
        public string SerialNumber { get; set; }

        [XmlElement(ElementName = "not-valid-after")]
        public string NotValidAfter { get; set; }

        [XmlElement(ElementName = "not-valid-before")]
        public string NotValidBefore { get; set; }

        [XmlElement(ElementName = "expiry-epoch")]
        public double ExpiryEpoch { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
