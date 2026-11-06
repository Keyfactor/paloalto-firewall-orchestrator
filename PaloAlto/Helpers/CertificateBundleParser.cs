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
        throw new NotImplementedException();
    }
}
