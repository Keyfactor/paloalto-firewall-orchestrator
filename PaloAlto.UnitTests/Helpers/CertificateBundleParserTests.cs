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

using System.Security.Cryptography.X509Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using PaloAlto.UnitTests.Generators;
using Xunit;

namespace PaloAlto.UnitTests.Helpers;

public class CertificateBundleParserTests
{
    private static readonly CertificateChainFixture Chain = new();

    private static void AssertLeaf(X509Certificate2 expected, string contents)
    {
        Assert.True(CertificateBundleParser.TryGetLeafCertificate(contents, out var leaf));
        Assert.NotNull(leaf);
        Assert.Equal(expected.RawData, leaf.RawData);
    }

    // ── Accepted formats ─────────────────────────────────────────────────────

    [Fact]
    public void TryGetLeafCertificate_SinglePem_ReturnsThatCertificate() =>
        AssertLeaf(Chain.Leaf, CertificateChainFixture.ToPem(Chain.Leaf));

    [Fact]
    public void TryGetLeafCertificate_BareBase64Der_ReturnsThatCertificate() =>
        AssertLeaf(Chain.Leaf, Convert.ToBase64String(Chain.Leaf.RawData));

    [Fact]
    public void TryGetLeafCertificate_PemWithCrLfAndSurroundingWhitespace_ReturnsThatCertificate()
    {
        var pem = "\r\n  " + CertificateChainFixture.ToPem(Chain.Leaf).Replace("\n", "\r\n") + "  \r\n";

        AssertLeaf(Chain.Leaf, pem);
    }

    [Fact]
    public void TryGetLeafCertificate_SelfSignedCertificate_ReturnsItself()
    {
        var selfSigned = CertificateChainFixture.SelfSigned("self-signed");

        AssertLeaf(selfSigned, CertificateChainFixture.ToPem(selfSigned));
    }

    // ── Bundles: the leaf is found regardless of order ───────────────────────

    [Fact]
    public void TryGetLeafCertificate_LeafThenIssuer_ReturnsTheLeaf() =>
        AssertLeaf(Chain.Leaf, CertificateChainFixture.Bundle(Chain.Leaf, Chain.Intermediate));

    [Fact]
    public void TryGetLeafCertificate_IssuerThenLeaf_ReturnsTheLeaf() =>
        AssertLeaf(Chain.Leaf, CertificateChainFixture.Bundle(Chain.Intermediate, Chain.Leaf));

    [Fact]
    public void TryGetLeafCertificate_FullChainInAnyOrder_ReturnsTheLeaf()
    {
        AssertLeaf(Chain.Leaf, CertificateChainFixture.Bundle(Chain.Leaf, Chain.Intermediate, Chain.Root));
        AssertLeaf(Chain.Leaf, CertificateChainFixture.Bundle(Chain.Root, Chain.Intermediate, Chain.Leaf));
        AssertLeaf(Chain.Leaf, CertificateChainFixture.Bundle(Chain.Intermediate, Chain.Leaf, Chain.Root));
    }

    [Fact]
    public void TryGetLeafCertificate_BundleWithoutALeaf_ReturnsTheCertificateNotIssuingAnother() =>
        AssertLeaf(Chain.Intermediate, CertificateChainFixture.Bundle(Chain.Root, Chain.Intermediate));

    [Fact]
    public void TryGetLeafCertificate_UnrelatedCertificates_FallsBackToTheFirst()
    {
        var first = CertificateChainFixture.SelfSigned("first");
        var second = CertificateChainFixture.SelfSigned("second");

        AssertLeaf(first, CertificateChainFixture.Bundle(first, second));
    }

    // ── Rejected input ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-certificate")]
    [InlineData("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN CERTIFICATE-----\n!!!not base64!!!\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN CERTIFICATE-----\nMIIB")]
    public void TryGetLeafCertificate_EmptyOrUnparseable_ReturnsFalseWithoutThrowing(string? contents)
    {
        Assert.False(CertificateBundleParser.TryGetLeafCertificate(contents, out var leaf));
        Assert.Null(leaf);
    }

    [Fact]
    public void TryGetLeafCertificate_Pfx_ReturnsFalse()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("CN=pfx", key, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var withKey = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var pfxBase64 = Convert.ToBase64String(withKey.Export(X509ContentType.Pfx, "password"));

        Assert.False(CertificateBundleParser.TryGetLeafCertificate(pfxBase64, out var leaf));
        Assert.Null(leaf);
    }
}
