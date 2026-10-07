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
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;

/// <summary>
/// A single attribute-type/value pair extracted from a subject's RDN sequence,
/// in declaration order. Multiple entries of the same type (e.g. repeated OU,
/// or even repeated CN) and multi-valued RDNs are all preserved — nothing is
/// collapsed into a dictionary.
/// </summary>
public sealed record SubjectAttribute(string TypeOid, string? TypeName, string Value, int RdnIndex, bool IsMultiValuedRdn);

// TODO: This would make a good candidate for a NuGet package
public class CertificateSubjectInformation
{
    // Keyed by OID string, never by friendly name — every RDN attribute has an OID,
    // not every OID has a known friendly name, so keying by name would silently drop
    // unrecognized attribute types and break losslessness.
    private readonly Dictionary<string, List<SubjectAttribute>> _attributesByOid;

    public CertificateSubjectInformation(string subjectDn)
    {
        var x509Name = new X509Name(subjectDn);
        _attributesByOid = new Dictionary<string, List<SubjectAttribute>>();

        var oids = x509Name.GetOidList();
        var values = x509Name.GetValueList();
        for (int i = 0; i < oids.Count; i++)
        {
            var oid = (DerObjectIdentifier)oids[i];
            var attr = new SubjectAttribute(
                oid.Id,
                X509Name.DefaultSymbols[oid] as string,
                (string)values[i],
                i,
                IsMultiValuedRdn: false); // see note below on RDN grouping

            if (!_attributesByOid.TryGetValue(oid.Id, out var list))
                _attributesByOid[oid.Id] = list = new List<SubjectAttribute>();
            list.Add(attr);
        }
    }

    // Generic, lossless access — works for any OID, known or not.
    public IReadOnlyList<SubjectAttribute> GetValues(string oidOrName)
    {
        var oid = ResolveOid(oidOrName);
        return _attributesByOid.TryGetValue(oid, out var list)
            ? list
            : Array.Empty<SubjectAttribute>();
    }

    public string? GetFirstValue(string oidOrName) => GetValues(oidOrName).FirstOrDefault()?.Value;

    // Convenience properties are just named shortcuts over the same store —
    // not a separate source of truth.
    
    /// <summary>
    /// The CN attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> CommonName => GetValues(X509Name.CN.Id);
    
    /// <summary>
    /// The O attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> Organization => GetValues(X509Name.O.Id);
    
    /// <summary>
    /// The OU attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> OrganizationalUnit => GetValues(X509Name.OU.Id);
    
    /// <summary>
    /// The L attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> CityLocality => GetValues(X509Name.L.Id);
    
    /// <summary>
    /// The ST attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> StateProvince => GetValues(X509Name.ST.Id);
    
    /// <summary>
    /// The C attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> CountryRegion => GetValues(X509Name.C.Id);
    
    /// <summary>
    /// The E attribute of the Certificate Subject Information, if present. Multiple entries are preserved in order.
    /// </summary>
    public IReadOnlyList<SubjectAttribute> Email => GetValues(X509Name.EmailAddress.Id);

    private static string ResolveOid(string oidOrName) =>
        X509Name.DefaultLookup[oidOrName.ToLowerInvariant()] is DerObjectIdentifier oid
            ? oid.Id
            : oidOrName; // assume caller passed a raw OID string already
}
