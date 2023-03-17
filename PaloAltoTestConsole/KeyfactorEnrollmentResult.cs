using System;
using System.Collections.Generic;
using System.Text;

namespace PaloAltoTestConsole
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class CertificateInformation
    {
        public string SerialNumber { get; set; }
        public string IssuerDN { get; set; }
        public string Thumbprint { get; set; }
        public int KeyfactorId { get; set; }
        public string Pkcs12Blob { get; set; }
        public object Password { get; set; }
        public int KeyfactorRequestId { get; set; }
        public string RequestDisposition { get; set; }
        public string DispositionMessage { get; set; }
        public object EnrollmentContext { get; set; }
    }

    public class Metadata
    {
    }

    public class KeyfactorEnrollmentResult
    {
        public CertificateInformation CertificateInformation { get; set; }
        public Metadata Metadata { get; set; }
    }
}
