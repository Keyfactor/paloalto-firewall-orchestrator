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
using System.Net.Http;
using Keyfactor.Logging;
using Keyfactor.PKI.CryptographicObjects;
using Keyfactor.PKI.Enums;
using Keyfactor.PKI.X509;
using Microsoft.Extensions.Logging;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public interface ICertificateChainService
{
    CertificateCollection BuildCertificateCollection(X509Certificate certificate);
}

public class CertificateChainService : ICertificateChainService
{
    private readonly ILogger _logger = LogHandler.GetClassLogger<CertificateChainService>();

    /// <summary>
    /// Builds a certificate collection containing the provided end entity certificate and its chain certificates, if available. If an error occurs during chain building, logs a warning and returns a collection with just the end entity certificate.
    /// NOTE: The ChainBuilder may have issues getting the trust store certificates on macOS. This does seem to work fine on Windows / Linux.
    /// </summary>
    /// <param name="certificate">A Org.BouncyCastle.X509 X509Certificate certificate</param>
    /// <returns></returns>
    public CertificateCollection BuildCertificateCollection(X509Certificate certificate)
    {
        try
        {
            _logger.LogTrace("Building certificate chain for certificate with serial number {serialNumber}",
                certificate.SerialNumber);

            using var http = new HttpClient();
            var store = new CertificateAuthorityCertificateCache();
            var builder = new ChainBuilder(store, http);

            _logger.LogTrace("ChainBuilder initialized. Calling BuildChain method on ChainBuilder for certificate with serial number {thumbprint}",
                certificate.SerialNumber);

            var chain = builder.BuildChain(certificate, CertificateCollectionOrder.EndEntityFirst);

            _logger.LogDebug(
                "Successfully built certificate chain for certificate with serial number {thumbprint}. Certificates count: {certificatesCount}, Chain count: {chainLength}",
                certificate.SerialNumber, chain.Certificates.Count(), chain.ChainCertificates.Count());

            LogChainContents(chain);

            return new CertificateCollection
            {
                EndEntityCert = certificate,
                ChainCerts = chain.ChainCertificates,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("An error occurred while building the certificate chain: {errorMessage}", ex.Message);
            
            // If a chain could not be built, at best return just the end entity cert.
            return new CertificateCollection()
            {
                EndEntityCert = certificate,
                ChainCerts = null
            };
        }
    }

    /// <summary>
    /// Log the contents of the CertificateChain at the Trace level, including subject DN and thumbprint for each certificate in both the Certificates and ChainCertificates collections.
    /// </summary>
    /// <param name="chain"></param>
    private void LogChainContents(CertificateChain chain)
    {
        for (var i = 0; i < chain.Certificates.Count(); i++)
        {
            var element = chain.Certificates.ElementAt(i);
            _logger.LogTrace(
                "Certificate element {index} in chain has subject DN {subject} and thumbprint {thumbprint}",
                i + 1, element.SubjectDN, element.SerialNumber);
        }
            
        for (var i = 0; i < chain.ChainCertificates.Count(); i++)
        {
            var element = chain.ChainCertificates.ElementAt(i);
            _logger.LogTrace(
                "ChainCertificate element {index} in chain has subject DN {subject} and thumbprint {thumbprint}",
                i + 1, element.SubjectDN, element.SerialNumber);
        }
    }
}

public class CertificateCollection
{
    public X509Certificate EndEntityCert { get; set; }
    public IEnumerable<X509Certificate>? ChainCerts { get; set; }
}