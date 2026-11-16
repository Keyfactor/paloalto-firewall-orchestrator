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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;

public class GenerateCertificateRequestMetadata
{
    public GenerateCertificateRequestAlgorithm? Algorithm { get; set; }
    public string Alias { get; set; }
    
    // Subject-level information
    public string CommonName { get; set; }
    public string? Email { get; set; }
    public string? State { get; set; }
    public string? Locality { get; set; }
    public string? Organization { get; set; }
    public string? OrganizationUnit { get; set; }
    public string? Country { get; set; }
    
    // Sans
    public List<string> EmailSans { get; set; } = new List<string>();
    public List<string> DnsSans { get; set; } = new List<string>();
    public List<string> IpSans { get; set; } = new List<string>();
    
    // For ODKG, these values need to be set
    private string _signedBy = "external";
    private string _ca = "no";

    /// <summary>
    /// Gets the Palo Alto XML request string for generating a certificate request based on the provided metadata.
    /// </summary>
    /// <returns></returns>
    public string GetCommand()
    {
        XElement generate = new XElement("generate",
            new XElement("signed-by", _signedBy),
            new XElement("ca", _ca),
            new XElement("certificate-name", Alias),
            new XElement("name", CommonName));

        if (Algorithm != null)
        {
            generate.Add(new XElement("algorithm", Algorithm.ToElement()));
        }

        if (EmailSans.Any())
        {
            generate.Add(new XElement("alt-email", EmailSans.Select(v => new XElement("member", v))));
        }

        if (DnsSans.Any())
        {
            generate.Add(new XElement("hostname", DnsSans.Select(v => new XElement("member", v))));
        }

        if (IpSans.Any())
        {
            generate.Add(new XElement("ip", IpSans.Select(v => new XElement("member", v))));
        }
        
        if (!string.IsNullOrEmpty(State)) generate.Add(new XElement("state", State));
        if (!string.IsNullOrEmpty(Locality)) generate.Add(new XElement("locality", Locality));
        if (!string.IsNullOrEmpty(Email)) generate.Add(new XElement("email", Email));
        if (!string.IsNullOrEmpty(Organization)) generate.Add(new XElement("organization", Organization));
        if (!string.IsNullOrEmpty(OrganizationUnit))
            generate.Add(new XElement("organization-unit", new XElement("member", OrganizationUnit)));
        if (!string.IsNullOrEmpty(Country)) generate.Add(new XElement("country-code", Country));
        
        XElement request = new XElement("request", new XElement("certificate", generate));
        return request.ToString(SaveOptions.DisableFormatting);
    }
}

public abstract class GenerateCertificateRequestAlgorithm
{
    public string Algorithm { get; set; }
    public string Size { get; init; }
    public abstract XElement ToElement();
}

public class RsaAlgorithm : GenerateCertificateRequestAlgorithm
{
    public RsaAlgorithm(string size)
    {
        Algorithm = "RSA";
        Size = size;
    }
    
    public override XElement ToElement() => new XElement("RSA", new XElement("rsa-nbits", Size));
}

public class EcdsaAlgorithm : GenerateCertificateRequestAlgorithm
{
    public EcdsaAlgorithm(string size)
    {
        Algorithm = "ECDSA";
        Size = size;
    }
    
    public override XElement ToElement() => new XElement("ECDSA", new XElement("ecdsa-nbits", Size));
}


