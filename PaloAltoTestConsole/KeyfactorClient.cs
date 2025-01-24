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
using System.Threading.Tasks;
using RestSharp;


namespace PaloAltoTestConsole
{
    public class KeyfactorClient
    {
        public async Task<KeyfactorEnrollmentResult> EnrollCertificate(string commonName)
        {
            var options = new RestClientOptions("https://bhillkf10.kfdelivery.com");
            var client = new RestClient(options);
            var request = new RestRequest("/KeyfactorAPI/Enrollment/PFX", Method.Post);
            request.AddHeader("X-Keyfactor-Requested-With", "APIClient");
            request.AddHeader("x-certificateformat", "PFX");
            request.AddHeader("Authorization", "Basic sdfa=");
            request.AddHeader("Content-Type", "application/json");
            var enrollRequest = new KeyfactorEnrollmentRequest
            {
                Password = "sldfklsdfsldjfk",
                PopulateMissingValuesFromAD = false,
                Subject = $"CN={commonName},C=US",
                IncludeChain = true,
                RenewalCertificateId = 0,
                CertificateAuthority = "DC-CA.Command.local\\CommandCA1",
                //CertificateAuthority = "brian-ejbca.kfdelivery.com\\MyPKISubCA-G1",
                Timestamp = DateTime.Now,
                Template = "2YearTestWebServer"
                //Template= "TLS Server Bhill_TLS Server BHill"
            };
            SANs sans = new SANs();
            List<string> dnsList = new List<string> { $"{commonName}" };
            sans.DNS = dnsList;
            enrollRequest.SANs = sans;
            request.AddBody(enrollRequest);
            var response = await client.ExecutePostAsync<KeyfactorEnrollmentResult>(request);
            return response.Data;

        }

    }
}
