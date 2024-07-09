// Copyright 2023 Keyfactor
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
// limitations under the License

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
        public string WorkflowInstanceId { get; set; }
        public int WorkflowReferenceId { get; set; }
        public List<object> StoreIdsInvalidForRenewal { get; set; }
        public int KeyfactorRequestId { get; set; }
        public string RequestDisposition { get; set; }
        public string DispositionMessage { get; set; }
        public object EnrollmentContext { get; set; }
    }

    public class Metadata
    {
        public string OID { get; set; }
    }

    public class KeyfactorEnrollmentResult
    {
        public CertificateInformation CertificateInformation { get; set; }
        public Metadata Metadata { get; set; }
    }


}
