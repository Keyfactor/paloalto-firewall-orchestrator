using System;
using System.Collections.Generic;
using System.Text;

namespace PaloAltoTestConsole
{
    public class KeyfactorEnrollmentRequest
    {
        public string CustomFriendlyName { get; set; }
        public string Password { get; set; }
        public bool PopulateMissingValuesFromAD { get; set; }
        public string Subject { get; set; }
        public bool IncludeChain { get; set; }
        public int RenewalCertificateId { get; set; }
        public string CertificateAuthority { get; set; }
        public DateTime Timestamp { get; set; }
        public string Template { get; set; }
        public SANs SANs { get; set; }
    }

    public class SANs
    {
        public List<string> DNS { get; set; }
    }
}
