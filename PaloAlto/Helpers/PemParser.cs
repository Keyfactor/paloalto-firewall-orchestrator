using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public class PemParser
{
    protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

    private readonly ILogger _logger;

    public PemParser(IClientLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PemParser>();
    }

    public string GetPemFile(string contents, string password, string certAlias)
    {
        _logger.MethodEntry();

        var pfxBytes = Convert.FromBase64String(contents);
        using var stream = new MemoryStream(pfxBytes);
        var store = new Pkcs12Store(stream, password?.ToCharArray() ?? Array.Empty<char>());

        _logger.LogTrace($"Loaded Pkcs12Store. Alias '{certAlias}' present: {store.ContainsAlias(certAlias)}");

        var keyAlias = store.Aliases.Cast<string>().SingleOrDefault(a => store.IsKeyEntry(a));
        _logger.LogTrace(keyAlias != null
            ? $"Key entry found under alias '{keyAlias}'"
            : "No key entry found — certificate-only import");

        var certChain = keyAlias != null
            ? store.GetCertificateChain(keyAlias)
            : store.Aliases.Cast<string>().Select(a => store.GetCertificate(a)).ToArray();

        var certPem = OrderCertificatesAndConvertToPem(certChain);

        if (keyAlias == null)
        {
            _logger.LogTrace("Returning certificate PEM without private key");
            _logger.MethodExit();
            return certPem;
        }

        KeyEntry = store.GetKey(keyAlias);
        if (KeyEntry == null) throw new Exception("Unable to retrieve private key");

        string privateKeyString;
        using (var memoryStream = new MemoryStream())
        using (TextWriter streamWriter = new StreamWriter(memoryStream))
        {
            var pemWriter = new PemWriter(streamWriter);
            var publicKey = store.GetCertificate(keyAlias).Certificate.GetPublicKey();
            var keyPair = new AsymmetricCipherKeyPair(publicKey, KeyEntry.Key);

            pemWriter.WriteObject(keyPair.Private);
            streamWriter.Flush();
            privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim()
                .Replace("\r", "").Replace("\0", "");
            _logger.LogTrace("Extracted private key");
        }

        _logger.MethodExit();
        return privateKeyString + certPem;
    }

    private string OrderCertificatesAndConvertToPem(X509CertificateEntry[] certificateEntries)
    {
        // Convert to X509Certificate objects for easier processing
        var certificates = certificateEntries
            .Select(entry => entry.Certificate)
            .ToList();

        // Create a dictionary to map Subject DN to certificate
        var subjectToCertificate = certificates.ToDictionary(cert => cert.SubjectDN.ToString());

        // Create a dictionary to map Issuer DN to subject DN
        var issuerToSubjects = certificates
            .GroupBy(cert => cert.IssuerDN.ToString())
            .ToDictionary(group => group.Key, group => group.Select(cert => cert.SubjectDN.ToString()).ToList());

        // Find the end-entity certificate: the cert whose SubjectDN is not used as an issuer by any other cert.
        // Fall back to the first cert when all certs are self-signed (e.g. a standalone root CA import).
        var endEntityCert =
            certificates.FirstOrDefault(cert => !issuerToSubjects.ContainsKey(cert.SubjectDN.ToString()))
            ?? certificates.First();

        // Build the chain from end-entity to root
        var orderedCertificates = new List<Org.BouncyCastle.X509.X509Certificate>();
        var currentCert = endEntityCert;

        while (currentCert != null)
        {
            orderedCertificates.Add(currentCert);
            var issuer = currentCert.IssuerDN.ToString();

            if (issuer == currentCert.SubjectDN.ToString()) // Self-signed certificate (root)
                break;

            currentCert = subjectToCertificate.ContainsKey(issuer) ? subjectToCertificate[issuer] : null;
        }

        // Convert the ordered certificates to a PEM string
        var pemString = string.Empty;

        foreach (var cert in orderedCertificates)
        {
            using (var stringWriter = new System.IO.StringWriter())
            {
                var pemWriter = new PemWriter(stringWriter);
                pemWriter.WriteObject(cert);
                pemWriter.Writer.Flush();
                pemString += stringWriter.ToString();
            }
        }

        return pemString;
    }
}
