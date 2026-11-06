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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PaloAlto.UnitTests.Generators;

/// <summary>
/// A public-only Root -> Intermediate -> Leaf chain (plus unrelated self-signed certificates) for tests that need to
/// reason about certificate bundles, as returned by Palo Alto's public-key element.
/// </summary>
public sealed class CertificateChainFixture
{
    public X509Certificate2 Root { get; }
    public X509Certificate2 Intermediate { get; }
    public X509Certificate2 Leaf { get; }

    public CertificateChainFixture()
    {
        using var rootKey = RSA.Create(2048);
        using var intermediateKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);

        var rootRequest = Request("CN=Test Root", rootKey, isCa: true);
        using var rootWithKey = rootRequest.CreateSelfSigned(notBefore, notBefore.AddYears(10));

        var intermediateRequest = Request("CN=Test Intermediate", intermediateKey, isCa: true);
        using var intermediatePublic = intermediateRequest.Create(rootWithKey, notBefore, notBefore.AddYears(5), Serial());
        using var intermediateWithKey = intermediatePublic.CopyWithPrivateKey(intermediateKey);

        var leafRequest = Request("CN=Test Leaf", leafKey, isCa: false);
        using var leafPublic = leafRequest.Create(intermediateWithKey, notBefore, notBefore.AddYears(1), Serial());

        // Public-only copies, safe to use after the keys above are disposed.
        Root = new X509Certificate2(rootWithKey.RawData);
        Intermediate = new X509Certificate2(intermediatePublic.RawData);
        Leaf = new X509Certificate2(leafPublic.RawData);
    }

    /// <summary>A certificate unrelated to the chain (its own issuer).</summary>
    public static X509Certificate2 SelfSigned(string commonName)
    {
        using var key = RSA.Create(2048);
        using var cert = Request($"CN={commonName}", key, isCa: false)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.RawData);
    }

    public static string ToPem(X509Certificate2 certificate) => certificate.ExportCertificatePem() + "\n";

    /// <summary>Concatenated PEM, in the order given (as Palo Alto returns a certificate with its issuers).</summary>
    public static string Bundle(params X509Certificate2[] certificates) =>
        string.Concat(certificates.Select(ToPem));

    private static CertificateRequest Request(string subject, RSA key, bool isCa)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, true));
        return request;
    }

    private static byte[] Serial() => RandomNumberGenerator.GetBytes(16);
}
