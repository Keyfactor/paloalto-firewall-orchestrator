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

namespace PaloAlto.UnitTests.Generators;

using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

public class CertificateGenerator
{
    /// <summary>
    /// Generates a self-signed, cross-platform X509Certificate2 object to be used in unit / integration tests.
    /// This eliminates the need to talk to a remote source (i.e. Keyfactor Command) to generate a compatible
    /// certificate to use for testing.
    /// </summary>
    /// <param name="alias"></param>
    /// <param name="password"></param>
    /// <param name="includeChain"></param>
    /// <returns></returns>
    public static X509Certificate2 GenerateCertificate(string alias, string password, bool includeChain)
    {
        var keyPair = GenerateKeyPair();

        var cert = CreateSelfSignedCert(alias, keyPair);

        if (includeChain)
        {
            return CreateChainedCertificate(alias, password, keyPair, cert);
        }

        return CreateX509FromSingleCert(alias, password, keyPair, cert);
    }

    private static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        return keyGen.GenerateKeyPair();
    }

    private static Org.BouncyCastle.X509.X509Certificate CreateSelfSignedCert(string alias, AsymmetricCipherKeyPair keyPair)
    {
        var certGen = new X509V3CertificateGenerator();
        var serial = BigInteger.ProbablePrime(120, new Random());
        var subject = new X509Name($"CN={alias}.example.com,C=US");

        certGen.SetSerialNumber(serial);
        certGen.SetSubjectDN(subject);
        certGen.SetIssuerDN(subject);
        certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certGen.SetNotAfter(DateTime.UtcNow.AddYears(1));
        certGen.SetPublicKey(keyPair.Public);

        certGen.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        certGen.AddExtension(X509Extensions.KeyUsage, false, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        return certGen.Generate(new Asn1SignatureFactory("SHA256withRSA", keyPair.Private));
    }

    private static X509Certificate2 CreateX509FromSingleCert(string alias, string password, AsymmetricCipherKeyPair keyPair, Org.BouncyCastle.X509.X509Certificate cert)
    {
        var store = new Pkcs12StoreBuilder().Build();
        var certEntry = new X509CertificateEntry(cert);
        store.SetKeyEntry(alias, new AsymmetricKeyEntry(keyPair.Private), new[] { certEntry });

        using var pfxStream = new MemoryStream();
        store.Save(pfxStream, password.ToCharArray(), new SecureRandom());
        return new X509Certificate2(pfxStream.ToArray(), password, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateChainedCertificate(string alias, string password, AsymmetricCipherKeyPair endEntityKeyPair, Org.BouncyCastle.X509.X509Certificate endEntityCert)
    {
        // Generate root
        var rootKeyPair = GenerateKeyPair();
        var rootCert = CreateCA("Root-" + alias, rootKeyPair, rootKeyPair.Private);

        // Generate intermediate
        var intermediateKeyPair = GenerateKeyPair();
        var intermediateCert = CreateCA("Intermediate-" + alias, intermediateKeyPair, rootKeyPair.Private, rootCert.SubjectDN);

        // Generate end-entity signed by intermediate
        var endCert = CreateEndEntitySigned(alias, endEntityKeyPair, intermediateKeyPair.Private, intermediateCert.SubjectDN);

        // Create PKCS12 store
        var store = new Pkcs12StoreBuilder().Build();

        var endEntry = new X509CertificateEntry(endCert);
        var intermediateEntry = new X509CertificateEntry(intermediateCert);
        var rootEntry = new X509CertificateEntry(rootCert);

        store.SetKeyEntry(alias, new AsymmetricKeyEntry(endEntityKeyPair.Private), new[] { endEntry, intermediateEntry, rootEntry });
        store.SetCertificateEntry("intermediate", intermediateEntry);
        store.SetCertificateEntry("root", rootEntry);

        using var pfxStream = new MemoryStream();
        store.Save(pfxStream, password.ToCharArray(), new SecureRandom());
        return new X509Certificate2(pfxStream.ToArray(), password, X509KeyStorageFlags.Exportable);
    }

    private static Org.BouncyCastle.X509.X509Certificate CreateCA(string cn, AsymmetricCipherKeyPair keyPair, AsymmetricKeyParameter signerKey, X509Name? issuer = null)
    {
        var certGen = new X509V3CertificateGenerator();
        var subject = new X509Name($"CN={cn}");
        var issuerName = issuer ?? subject;

        certGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
        certGen.SetSubjectDN(subject);
        certGen.SetIssuerDN(issuerName);
        certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certGen.SetNotAfter(DateTime.UtcNow.AddYears(10));
        certGen.SetPublicKey(keyPair.Public);
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));

        return certGen.Generate(new Asn1SignatureFactory("SHA256withRSA", signerKey));
    }

    private static Org.BouncyCastle.X509.X509Certificate CreateEndEntitySigned(string alias, AsymmetricCipherKeyPair keyPair, AsymmetricKeyParameter signerKey, X509Name issuer)
    {
        var certGen = new X509V3CertificateGenerator();
        var subject = new X509Name($"CN={alias}.example.com,C=US");

        certGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
        certGen.SetSubjectDN(subject);
        certGen.SetIssuerDN(issuer);
        certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certGen.SetNotAfter(DateTime.UtcNow.AddYears(1));
        certGen.SetPublicKey(keyPair.Public);
        certGen.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        certGen.AddExtension(X509Extensions.KeyUsage, false, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        return certGen.Generate(new Asn1SignatureFactory("SHA256withRSA", signerKey));
    }
}
