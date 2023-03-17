using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RestSharp;


namespace PaloAltoTestConsole
{
    public class KeyfactorClient
    {
        public async Task<KeyfactorEnrollmentResult> EnrollCertificate(string commonName)
        {
            var options = new RestClientOptions("https://URLToKeyfactor");
            var client = new RestClient(options);
            var request = new RestRequest("/KeyfactorAPI/Enrollment/PFX", Method.Post);
            request.AddHeader("X-Keyfactor-Requested-With", "APIClient");
            request.AddHeader("x-certificateformat", "PFX");
            request.AddHeader("Authorization", "Basic BasicAuthKey");
            request.AddHeader("Content-Type", "application/json");
            var enrollRequest = new KeyfactorEnrollmentRequest
            {
                CustomFriendlyName = "2 Year Web Server",
                Password = "sldfklsdfsldjfk",
                PopulateMissingValuesFromAD = false,
                Subject = $"CN={commonName}",
                IncludeChain = true,
                RenewalCertificateId = 0,
                CertificateAuthority = "DC-CA.Command.local\\CommandCA1",
                Timestamp = DateTime.Now,
                Template = "2YearTestWebServer"
            };
            SANs sans = new SANs();
            List<string> dnsList = new List<string> { $"{commonName}" };
            sans.DNS = dnsList;
            enrollRequest.SANs = sans;
            request.AddBody(enrollRequest);
            var response = await client.ExecuteAsync<KeyfactorEnrollmentResult>(request);
            return response.Data;

        }

    }
}
