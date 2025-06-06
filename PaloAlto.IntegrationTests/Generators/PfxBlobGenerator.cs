using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1.X509;

public static class PfxGenerator
{
    // /// <summary>
    // /// Generates a PFX blob with proper alias set (cross-platform)
    // /// </summary>
    // public static string GetBlob(string alias, string password)
    // {
    //     return CreatePfxWithBouncyCastle(alias, password, false);
    // }

    /// <summary>
    /// Generates a PFX blob with certificate chain and proper aliases (cross-platform)
    /// </summary>
    public static string GetBlobWithChain(string alias, string password)
    {
        return CreatePfxWithBouncyCastle(alias, password, true);
    }

    /// <summary>
    /// Creates PFX using BouncyCastle for cross-platform alias support
    /// </summary>
    private static string CreatePfxWithBouncyCastle(string alias, string password, bool includeChain)
    {
        // Generate key pair
        var keyGenerator = new RsaKeyPairGenerator();
        keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var keyPair = keyGenerator.GenerateKeyPair();

        // Create certificate generator
        var certificateGenerator = new X509V3CertificateGenerator();
        var serialNumber = BigInteger.ProbablePrime(120, new Random());
        
        certificateGenerator.SetSerialNumber(serialNumber);
        certificateGenerator.SetSubjectDN(new X509Name($"CN={alias}.example.com,C=US"));
        certificateGenerator.SetIssuerDN(new X509Name($"CN={alias}.example.com,C=US")); // Self-signed
        certificateGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certificateGenerator.SetNotAfter(DateTime.UtcNow.AddDays(365));
        certificateGenerator.SetPublicKey(keyPair.Public);
        certificateGenerator.SetSignatureAlgorithm("SHA256withRSA");

        // Add extensions
        certificateGenerator.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        certificateGenerator.AddExtension(X509Extensions.KeyUsage, false, 
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        // Sign the certificate
        var certificate = certificateGenerator.Generate(keyPair.Private);

        if (includeChain)
        {
            return CreatePfxWithChainBouncyCastle(alias, password, keyPair, certificate);
        }
        else
        {
            return CreateSingleCertificatePfx(alias, password, keyPair, certificate);
        }
    }

    private static string CreateSingleCertificatePfx(string alias, string password, AsymmetricCipherKeyPair keyPair, Org.BouncyCastle.X509.X509Certificate certificate)
    {
        // Create PKCS12 store
        var store = new Pkcs12StoreBuilder().Build();
        
        // Create certificate entry
        var certEntry = new X509CertificateEntry(certificate);
        
        // Create key entry with the private key
        var keyEntry = new AsymmetricKeyEntry(keyPair.Private);
        
        // Add certificate and key to store with the specified alias
        store.SetCertificateEntry(alias, certEntry);
        store.SetKeyEntry(alias, keyEntry, new[] { certEntry });

        // Export as PFX bytes
        using var pfxStream = new MemoryStream();
        store.Save(pfxStream, password.ToCharArray(), new SecureRandom());
        
        var pfxBytes = pfxStream.ToArray();
        return Convert.ToBase64String(pfxBytes);
    }

    private static string CreatePfxWithChainBouncyCastle(string alias, string password, AsymmetricCipherKeyPair endEntityKeyPair, Org.BouncyCastle.X509.X509Certificate endEntityCert)
    {
        // Create root CA
        var rootKeyPair = new RsaKeyPairGenerator();
        rootKeyPair.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var rootKeys = rootKeyPair.GenerateKeyPair();

        var rootCertGen = new X509V3CertificateGenerator();
        rootCertGen.SetSignatureAlgorithm("SHA256withRSA");
        rootCertGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
        rootCertGen.SetSubjectDN(new X509Name($"CN=Root-{alias}"));
        rootCertGen.SetIssuerDN(new X509Name($"CN=Root-{alias}"));
        rootCertGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        rootCertGen.SetNotAfter(DateTime.UtcNow.AddDays(3650));
        rootCertGen.SetPublicKey(rootKeys.Public);
        rootCertGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        var rootCert = rootCertGen.Generate(rootKeys.Private);

        // Create intermediate CA
        var intKeyPair = new RsaKeyPairGenerator();
        intKeyPair.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var intKeys = intKeyPair.GenerateKeyPair();

        var intCertGen = new X509V3CertificateGenerator();
        intCertGen.SetSignatureAlgorithm("SHA256withRSA");
        intCertGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
        intCertGen.SetSubjectDN(new X509Name($"CN=Intermediate-{alias}"));
        intCertGen.SetIssuerDN(new X509Name($"CN=Root-{alias}"));
        intCertGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        intCertGen.SetNotAfter(DateTime.UtcNow.AddDays(1825));
        intCertGen.SetPublicKey(intKeys.Public);
        intCertGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        var intCert = intCertGen.Generate(rootKeys.Private);

        // Update end entity to be signed by intermediate
        var endCertGen = new X509V3CertificateGenerator();
        endCertGen.SetSignatureAlgorithm("SHA256withRSA");
        endCertGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
        endCertGen.SetSubjectDN(new X509Name($"CN={alias}.example.com,C=US"));
        endCertGen.SetIssuerDN(new X509Name($"CN=Intermediate-{alias}"));
        endCertGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        endCertGen.SetNotAfter(DateTime.UtcNow.AddDays(365));
        endCertGen.SetPublicKey(endEntityKeyPair.Public);
        endCertGen.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        var finalEndCert = endCertGen.Generate(intKeys.Private);

        // Create PKCS12 store with chain
        var store = new Pkcs12StoreBuilder().Build();
        
        // Add certificates to store
        var endCertEntry = new X509CertificateEntry(finalEndCert);
        var intCertEntry = new X509CertificateEntry(intCert);
        var rootCertEntry = new X509CertificateEntry(rootCert);
        
        // Add key entry for end entity certificate
        var keyEntry = new AsymmetricKeyEntry(endEntityKeyPair.Private);
        
        // Set the main certificate with the specified alias
        store.SetKeyEntry(alias, keyEntry, new[] { endCertEntry, intCertEntry, rootCertEntry });
        
        // Add individual certificate entries (optional, but helps with compatibility)
        store.SetCertificateEntry($"{alias}-cert", endCertEntry);
        store.SetCertificateEntry($"{alias}-intermediate", intCertEntry);
        store.SetCertificateEntry($"{alias}-root", rootCertEntry);

        // Export as PFX bytes
        using var pfxStream = new MemoryStream();
        store.Save(pfxStream, password.ToCharArray(), new SecureRandom());
        
        var pfxBytes = pfxStream.ToArray();
        return Convert.ToBase64String(pfxBytes);
    }

    /// <summary>
    /// Cross-platform method to set alias after certificate creation (fallback for .NET certificates)
    /// </summary>
    public static string SetAliasInExistingPfx(byte[] pfxBytes, string currentPassword, string alias, string newPassword = null)
    {
        var outputPassword = newPassword ?? currentPassword;
        
        // Load existing PFX into BouncyCastle store
        using var inputStream = new MemoryStream(pfxBytes);
        var store = new Pkcs12Store(inputStream, currentPassword.ToCharArray());
        
        // Create new store with proper alias
        var newStore = new Pkcs12StoreBuilder().Build();
        
        // Find the key entry (certificate with private key)
        var keyAlias = store.Aliases.Cast<string>().FirstOrDefault(a => store.IsKeyEntry(a));
        if (keyAlias != null)
        {
            var keyEntry = store.GetKey(keyAlias);
            var certChain = store.GetCertificateChain(keyAlias);
            
            // Add with new alias
            newStore.SetKeyEntry(alias, keyEntry, certChain);
            
            // Add individual certificates
            for (int i = 0; i < certChain.Length; i++)
            {
                var certAlias = i == 0 ? alias : $"{alias}-{i}";
                newStore.SetCertificateEntry(certAlias, certChain[i]);
            }
        }
        
        // Copy other certificate entries
        foreach (string storeAlias in store.Aliases)
        {
            if (!store.IsKeyEntry(storeAlias))
            {
                var certEntry = store.GetCertificate(storeAlias);
                newStore.SetCertificateEntry(storeAlias, certEntry);
            }
        }
        
        // Export new PFX
        using var outputStream = new MemoryStream();
        newStore.Save(outputStream, outputPassword.ToCharArray(), new SecureRandom());
        
        return Convert.ToBase64String(outputStream.ToArray());
    }


}