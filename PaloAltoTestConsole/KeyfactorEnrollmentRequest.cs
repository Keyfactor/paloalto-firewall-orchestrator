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
// limitations under the License.

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
