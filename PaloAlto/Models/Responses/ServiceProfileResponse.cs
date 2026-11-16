// Copyright 2026 Keyfactor
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

using System.Collections.Generic;
using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;

[XmlRoot(ElementName = "response")]
public class ServiceProfileResponse
{
    [XmlElement(ElementName = "result")]
    public ServiceProfileResult Result { get; set; }

    [XmlAttribute(AttributeName = "status")]
    public string Status { get; set; }

    [XmlAttribute(AttributeName = "code")]
    public int Code { get; set; }
}

[XmlRoot(ElementName = "result")]
public class ServiceProfileResult
{
    [XmlElement(ElementName = "ssl-tls-service-profile")]
    public SslTlsServiceProfile SslTlsServiceProfile { get; set; }

    [XmlAttribute(AttributeName = "count")]
    public int Count { get; set; }

    [XmlAttribute(AttributeName = "total-count")]
    public int TotalCount { get; set; }
}

[XmlRoot(ElementName = "ssl-tls-service-profile")]
public class SslTlsServiceProfile : TemplateSource
{
    [XmlElement(ElementName = "entry")]
    public List<ServiceProfileEntry> Entries { get; set; } = new();
}

[XmlRoot(ElementName = "entry")]
public class ServiceProfileEntry : TemplateSource
{
    [XmlAttribute(AttributeName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// Null when the profile has no protocol-settings element. May be present but empty (all child values null).
    /// </summary>
    [XmlElement(ElementName = "protocol-settings")]
    public ProtocolSettings ProtocolSettings { get; set; }

    /// <summary>
    /// The name of the certificate bound to the profile.
    /// </summary>
    [XmlElement(ElementName = "certificate")]
    public TemplateValue Certificate { get; set; }
}

[XmlRoot(ElementName = "protocol-settings")]
public class ProtocolSettings : TemplateSource
{
    [XmlElement(ElementName = "min-version")]
    public TemplateValue MinVersion { get; set; }

    [XmlElement(ElementName = "max-version")]
    public TemplateValue MaxVersion { get; set; }

    [XmlElement(ElementName = "keyxchg-algo-rsa")]
    public TemplateValue KeyExchangeAlgoRsa { get; set; }

    [XmlElement(ElementName = "keyxchg-algo-dhe")]
    public TemplateValue KeyExchangeAlgoDhe { get; set; }

    [XmlElement(ElementName = "keyxchg-algo-ecdhe")]
    public TemplateValue KeyExchangeAlgoEcdhe { get; set; }

    [XmlElement(ElementName = "enc-algo-aes-128-cbc")]
    public TemplateValue EncAlgoAes128Cbc { get; set; }

    [XmlElement(ElementName = "enc-algo-aes-128-gcm")]
    public TemplateValue EncAlgoAes128Gcm { get; set; }

    [XmlElement(ElementName = "enc-algo-aes-chacha20-poly1305")]
    public TemplateValue EncAlgoAesChacha20Poly1305 { get; set; }

    [XmlElement(ElementName = "enc-algo-aes-256-cbc")]
    public TemplateValue EncAlgoAes256Cbc { get; set; }

    [XmlElement(ElementName = "enc-algo-aes-256-gcm")]
    public TemplateValue EncAlgoAes256Gcm { get; set; }

    [XmlElement(ElementName = "auth-algo-sha1")]
    public TemplateValue AuthAlgoSha1 { get; set; }

    [XmlElement(ElementName = "auth-algo-sha256")]
    public TemplateValue AuthAlgoSha256 { get; set; }

    [XmlElement(ElementName = "auth-algo-sha384")]
    public TemplateValue AuthAlgoSha384 { get; set; }
}

/// <summary>
/// A leaf element whose text is the value, carrying the Panorama template attributes.
/// </summary>
public class TemplateValue : TemplateSource
{
    [XmlText]
    public string Value { get; set; }
}

/// <summary>
/// Attributes Panorama adds to elements returned from a template or template stack
/// (ptpl = the template/stack the setting came from, src = its source).
/// </summary>
public class TemplateSource
{
    [XmlAttribute(AttributeName = "ptpl")]
    public string Ptpl { get; set; }

    [XmlAttribute(AttributeName = "src")]
    public string Src { get; set; }
}
