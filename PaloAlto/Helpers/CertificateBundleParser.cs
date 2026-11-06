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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

/// <summary>
/// Extracts the end-entity (leaf) certificate from certificate text, so a certificate supplied by a job can be
/// compared with the certificates stored on a Palo Alto device.
/// </summary>
public static class CertificateBundleParser
{
    /// <summary>
    /// Parses <paramref name="contents"/> and returns its end-entity certificate.
    /// <para>
    /// Accepted input: one or more PEM certificates (with any line endings / surrounding whitespace), or a bare
    /// base64-encoded DER certificate. PFX / PKCS#12 data is not accepted.
    /// </para>
    /// <para>
    /// For a bundle, the leaf is the certificate that is not the issuer of any other certificate in the bundle (a
    /// certificate is an issuer of another when the other's issuer name equals its subject name), regardless of order.
    /// If no single certificate qualifies, the first certificate in the bundle is returned.
    /// </para>
    /// The caller owns (and should dispose) the returned certificate.
    /// </summary>
    /// <returns>false if the contents are empty or cannot be parsed as certificate(s); never throws for bad input.</returns>
    public static bool TryGetLeafCertificate(string? contents, out X509Certificate2? leaf)
    {
        leaf = null;
        if (string.IsNullOrWhiteSpace(contents)) return false;

        var certificates = new List<X509Certificate2>();
        try
        {
            if (contents.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                var collection = new X509Certificate2Collection();
                collection.ImportFromPem(contents);
                certificates.AddRange(collection);
            }
            else
            {
                var der = Convert.FromBase64String(string.Concat(contents.Where(c => !char.IsWhiteSpace(c))));

                // Only a plain certificate; PFX / PKCS#12 (which can hold a private key) must not be accepted.
                if (X509Certificate2.GetCertContentType(der) != X509ContentType.Cert) return false;

                certificates.Add(new X509Certificate2(der));
            }
        }
        catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException)
        {
            DisposeAll(certificates);
            return false;
        }

        if (certificates.Count == 0) return false;

        var selected = SelectLeaf(certificates);
        DisposeAll(certificates.Where(c => !ReferenceEquals(c, selected)));
        leaf = selected;
        return true;
    }

    private static X509Certificate2 SelectLeaf(List<X509Certificate2> certificates)
    {
        // A certificate that issues another certificate in the bundle is not an end-entity certificate.
        var leaves = certificates
            .Where(candidate => !certificates.Any(other =>
                !ReferenceEquals(other, candidate) &&
                other.IssuerName.RawData.AsSpan().SequenceEqual(candidate.SubjectName.RawData)))
            .ToList();

        return leaves.Count == 1 ? leaves[0] : certificates[0];
    }

    private static void DisposeAll(IEnumerable<X509Certificate2> certificates)
    {
        foreach (var certificate in certificates) certificate.Dispose();
    }
}
