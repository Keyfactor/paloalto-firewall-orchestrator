using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Newtonsoft.Json;
using RestSharp;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client
{
    public class PaloAltoClient
    {

        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private string ApiKey { get; set; }
        private string Url { get; set; }

        public PaloAltoClient(string url,string key)
        {
            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };
            Url = url;
            HttpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri(url)};
            ApiKey = key;
        }

        private HttpClient HttpClient { get; }

        public async Task<CertificateListResponse> GetCertificateList()
        {
            var uri = $"/api/?type=config&action=get&xpath=/config/shared/certificate&key={ApiKey}";
            var response= await GetXmlResponseAsync<CertificateListResponse>(await HttpClient.GetAsync(uri));
            return response;
        }

        public async Task<TrustedRootListResponse> GetTrustedRootList()
        {
            var uri = $"/api/?type=config&action=get&xpath=/config/predefined/trusted-root-ca&key={ApiKey}";
            return await GetXmlResponseAsync<TrustedRootListResponse>(await HttpClient.GetAsync(uri));
        }

        public async Task<string> GetCertificateByName(string name)
        {
            var uri = $@"/api/?type=export&category=certificate&certificate-name={name}&format=pem&include-key=no&key={ApiKey}";
            return await GetResponseAsync<string>(await HttpClient.GetAsync(uri));
        }

        public async Task<string> SubmitImportCertificate(string name)
        {

            var uri = $@"/api/?type=export&category=certificate&certificate-name={name}&format=pem&include-key=no&key={ApiKey}";
            return await GetResponseAsync<string>(await HttpClient.GetAsync(uri));
        }

        public async Task<ImportCertificateResponse> ImportCertificate(string name,string passPhrase,byte[] bytes)
        {
            var uri = $@"/api/?type=import&category=keypair&certificate-name={name}&format=pem&include-key=yes&passphrase={passPhrase}&target-tpl=&target-tpl-vsys=&vsys&key={ApiKey}";
            var boundary = $"--------------------------598408616359830956846110";
            var requestContent = new MultipartFormDataContent();
            requestContent.Headers.Remove("Content-Type");
            requestContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");
            //Workaround Palo Alto API does not like double quotes around boundary so can't use built in .net client
            requestContent.GetType().BaseType.GetField("_boundary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(requestContent, boundary);
            var pfxContent=new ByteArrayContent(bytes);
            pfxContent.Headers.ContentType=MediaTypeHeaderValue.Parse("application/x-x509-ca-cert");
            requestContent.Add(pfxContent,"\"file\"","\"TestUploadCode.pem\"");
            return await GetXmlResponseAsync<ImportCertificateResponse>(await HttpClient.PostAsync(uri, requestContent));
            
        }


        private async Task<T> GetXmlResponseAsync<T>(HttpResponseMessage response)
        {
            EnsureSuccessfulResponse(response);
            var stringResponse = await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
            XmlSerializer serializer =
                new XmlSerializer(typeof(T));
            XmlReader xmlReader = XmlReader.Create(new StringReader(stringResponse));
            return (T)serializer.Deserialize(xmlReader);
        }

        private async Task<string> GetResponseAsync<T>(HttpResponseMessage response)
        {
            EnsureSuccessfulResponse(response);
            var stringResponse = await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
            return stringResponse;
        }

        

        private void EnsureSuccessfulResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = new StreamReader(response.Content.ReadAsStreamAsync().Result).ReadToEnd();
                throw new Exception($"Request to PaloAlto was not successful - {response.StatusCode} - {error}");
            }
        }


    }
}