using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client
{
    public class PaloAltoClient
    {
        private string ApiKey { get; set; }

        private readonly ILogger<PaloAltoClient> _logger;

        public PaloAltoClient(ILogger<PaloAltoClient> logger)
        {
            _logger = logger;
        }

        public PaloAltoClient(string url,string key)
        {
            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };
            HttpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri("https://" + url)};
            ApiKey = key;
        }

        private HttpClient HttpClient { get; }

        public async Task<CertificateListResponse> GetCertificateList()
        {
            try
            {
                var uri = $"/api/?type=config&action=get&xpath=/config/shared/certificate&key={ApiKey}";
                var response= await GetXmlResponseAsync<CertificateListResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateList: {e.Message}");
                throw;
            }
        }

        public async Task<TrustedRootListResponse> GetTrustedRootList()
        {
            try
            {
                var uri = $"/api/?type=config&action=get&xpath=/config/predefined/trusted-root-ca&key={ApiKey}";
                return await GetXmlResponseAsync<TrustedRootListResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetTrustedRootList: {e.Message}");
                throw;
            }
        }

        public async Task<string> GetCertificateByName(string name)
        {
            try
            {
                var uri = $@"/api/?type=export&category=certificate&certificate-name={name}&format=pem&include-key=no&key={ApiKey}";
                return await GetResponseAsync(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateByName: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitDeleteCertificate(string name)
        {

            try
            {
                var uri = $@"/api/?type=config&action=delete&xpath=/config/shared/certificate/entry[@name='{name}']&key={ApiKey}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitSetTrustedRoot(string name)
        {

            try
            {
                var uri = $@"/api/?type=config&action=set&xpath=/config/shared/ssl-decrypt&element=<trusted-root-CA><member>{name}</member></trusted-root-CA>&key={ApiKey}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitSetForwardTrust(string name)
        {

            try
            {
                var uri = $@"/api/?type=config&action=set&xpath=/config/shared/ssl-decrypt&element=<forward-trust-certificate><rsa>{name}</rsa></forward-trust-certificate>&key={ApiKey}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ImportCertificateResponse> ImportCertificate(string name,string passPhrase,byte[] bytes,string includeKey,string category)
        {
            try
            {
                var uri = $@"/api/?type=import&category={category}&certificate-name={name}&format=pem&include-key={includeKey}&passphrase={passPhrase}&target-tpl=&target-tpl-vsys=&vsys&key={ApiKey}";
                var boundary = $"--------------------------{Guid.NewGuid():N}";
                var requestContent = new MultipartFormDataContent();
                requestContent.Headers.Remove("Content-Type");
                requestContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");
                //Workaround Palo Alto API does not like double quotes around boundary so can't use built in .net client
                requestContent.GetType().BaseType?.GetField("_boundary", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(requestContent, boundary);
                var pfxContent=new ByteArrayContent(bytes);
                pfxContent.Headers.ContentType=MediaTypeHeaderValue.Parse("application/x-x509-ca-cert");
                requestContent.Add(pfxContent,"\"file\"", $"\"{name}.pem\"");
                return await GetXmlResponseAsync<ImportCertificateResponse>(await HttpClient.PostAsync(uri, requestContent));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.ImportCertificate: {e.Message}");
                throw;
            }
            
        }


        private async Task<T> GetXmlResponseAsync<T>(HttpResponseMessage response)
        {
            try
            {
                EnsureSuccessfulResponse(response);
                var stringResponse = await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
                XmlSerializer serializer =
                    new XmlSerializer(typeof(T));
                XmlReader xmlReader = XmlReader.Create(new StringReader(stringResponse));
                return (T)serializer.Deserialize(xmlReader);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetXmlResponseAsync: {e.Message}");
                throw;
            }
        }

        private async Task<string> GetResponseAsync(HttpResponseMessage response)
        {
            try
            {
                EnsureSuccessfulResponse(response);
                var stringResponse = await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
                return stringResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetResponseAsync: {e.Message}");
                throw;
            }
        }
        
        private void EnsureSuccessfulResponse(HttpResponseMessage response)
        {
            try
            {
                if (!response.IsSuccessStatusCode)
                {
                    var error = new StreamReader(response.Content.ReadAsStreamAsync().Result).ReadToEnd();
                    throw new Exception($"Request to PaloAlto was not successful - {response.StatusCode} - {error}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.EnsureSuccessfulResponse: {e.Message}");
                throw;
            }
        }


    }
}