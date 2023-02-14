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
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Requests;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client
{
    public class PaloAltoClient
    {
        private readonly ILogger _logger;

        public PaloAltoClient(string url, string userName, string password)
        {
            _logger = LogHandler.GetClassLogger<PaloAltoClient>();
            ServerUserName = userName;
            ServerPassword = password;
            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };
            HttpClient = new HttpClient(httpClientHandler) {BaseAddress = new Uri("https://" + url)};

            ApiKey = GetAuthenticationResponse().Result?.Result?.Key;
        }

        private string ApiKey { get; }

        private string ServerPassword { get; }

        private string ServerUserName { get; }

        private HttpClient HttpClient { get; }

        public async Task<CertificateListResponse> GetCertificateList(string path)
        {
            try
            {
                //path = System.Web.HttpUtility.UrlEncode(path);
                var uri = $"/api/?type=config&action=get&xpath={path}&key={ApiKey}";
                var response = await GetXmlResponseAsync<CertificateListResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateList: {e.Message}");
                throw;
            }
        }

        public async Task<NamedListResponse> GetTemplateList()
        {
            try
            {
                var uri =
                    $"/api/?type=config&action=get&xpath=/config/devices/entry[@name='localhost.localdomain']/template/entry/@name&key={ApiKey}";
                var response = await GetXmlResponseAsync<NamedListResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetTemplateList: {e.Message}");
                throw;
            }
        }

        public async Task<NamedListResponse> GetDeviceGroupList()
        {
            try
            {
                var uri =
                    $"/api/?type=config&action=get&xpath=/config/devices/entry[@name='localhost.localdomain']/device-group/entry/@name&key={ApiKey}";
                var response = await GetXmlResponseAsync<NamedListResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetDeviceGroupList: {e.Message}");
                throw;
            }
        }

        public async Task<CommitResponse> GetCommitResponse()
        {
            try
            {
                var uri =
                    $"/api/?&type=commit&action=partial&cmd=<commit><partial><admin><member>{ServerUserName}</member></admin></partial></commit>&key={ApiKey}";

                var response = await GetXmlResponseAsync<CommitResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateList: {e.Message}");
                throw;
            }
        }

        public async Task<CommitResponse> GetCommitAllResponse(string deviceGroup)
        {
            try
            {
                //Palo alto claims this commented out line works for push to devices by userid but can't get this to work
                //var uri = $"/api/?&type=commit&action=all&cmd=<commit-all><shared-policy><admin><member>{ServerUserName}</member></admin><device-group><entry name=\"{deviceGroup}\"/></device-group></shared-policy></commit-all>&key={ApiKey}";
                var uri =
                    $"/api/?&type=commit&action=all&cmd=<commit-all><shared-policy><device-group><entry name=\"{deviceGroup}\"/></device-group></shared-policy></commit-all>&key={ApiKey}";
                var response = await GetXmlResponseAsync<CommitResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateList: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitEditProfile(EditProfileRequest request, string templateName)
        {
            try
            {
                var editXml =
                    $"<entry name=\"{request.Name}\"><protocol-settings><min-version>{request.ProtocolSettings.MinVersion.Text}</min-version><max-version>{request.ProtocolSettings.MaxVersion.Text}</max-version></protocol-settings><certificate>{request.Certificate}</certificate></entry>";
                string uri;

                //if not Panorama use firewall path
                if (templateName == "/")
                {
                    templateName = "";
                    uri =
                        $@"/api/?type=config&action=edit&xpath=/config/shared/ssl-tls-service-profile/entry[@name='{request.Name}']&element={editXml}&key={ApiKey}&target-tpl={templateName}";
                }
                else
                {
                    uri =
                        $@"/api/?type=config&action=edit&xpath=/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='{templateName}']/config/shared/ssl-tls-service-profile/entry&element={editXml}&key={ApiKey}&target-tpl={templateName}";
                }

                var response = await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<GetProfileByCertificateResponse> GetProfileByCertificate(string templateName,
            string certificate)
        {
            try
            {
                var xPath = templateName == "/"
                    ? $"/config/shared/ssl-tls-service-profile/entry[@name='{certificate}']"
                    : $"/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='{templateName}']/config/shared/ssl-tls-service-profile/entry[./certificate='{certificate}']";
                var uri = $"/api/?type=config&action=get&target-tpl={templateName}&xpath={xPath}&key={ApiKey}";
                var response =
                    await GetXmlResponseAsync<GetProfileByCertificateResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetProfileByCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<AuthenticationResponse> GetAuthenticationResponse()
        {
            try
            {
                var uri = $"/api/?type=keygen&user={ServerUserName}&password={ServerPassword}";
                var response = await GetXmlResponseAsync<AuthenticationResponse>(await HttpClient.GetAsync(uri));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetAuthenticationResponse: {e.Message}");
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
                var uri =
                    $@"/api/?type=export&category=certificate&certificate-name={name}&format=pem&include-key=no&key={ApiKey}";
                return await GetResponseAsync(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateByName: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitDeleteCertificate(string name, string templateName)
        {
            try
            {
                string uri;
                if (templateName == "/")
                {
                    templateName = "";
                    uri =
                        $@"/api/?type=config&action=delete&xpath=/config/shared/certificate/entry[@name='{name}']&key={ApiKey}&target-tpl={templateName}";
                }
                else
                {
                    uri =
                        $@"/api/?type=config&action=delete&xpath=/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared/certificate/entry[@name='{name}']&key={ApiKey}&target-tpl={templateName}";
                }

                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitSetTrustedRoot(string name, string templateName)
        {
            try
            {
                string uri;
                if (templateName == "/")
                {
                    templateName = "";
                    uri =
                        $@"/api/?type=config&action=set&xpath=/config/shared/ssl-decrypt&element=<trusted-root-CA><member>{name}</member></trusted-root-CA>&key={ApiKey}&target-tpl={templateName}";
                }
                else
                {
                    uri =
                        $@"/api/?type=config&action=set&xpath=/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='{templateName}']/config/shared/ssl-decrypt&element=<trusted-root-CA><member>{name}</member></trusted-root-CA>&key={ApiKey}&target-tpl={templateName}";
                }

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
                var uri =
                    $@"/api/?type=config&action=set&xpath=/config/shared/ssl-decrypt&element=<forward-trust-certificate><rsa>{name}</rsa></forward-trust-certificate>&key={ApiKey}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ImportCertificateResponse> ImportCertificate(string name, string passPhrase, byte[] bytes,
            string includeKey, string category, string templateName)
        {
            try
            {
                if (templateName == "/")
                    templateName = "";
                var uri =
                    $@"/api/?type=import&category={category}&certificate-name={name}&format=pem&include-key={includeKey}&passphrase={passPhrase}&target-tpl={templateName}&target-tpl-vsys=&vsys&key={ApiKey}";
                var boundary = $"--------------------------{Guid.NewGuid():N}";
                var requestContent = new MultipartFormDataContent();
                requestContent.Headers.Remove("Content-Type");
                requestContent.Headers.TryAddWithoutValidation("Content-Type",
                    $"multipart/form-data; boundary={boundary}");
                //Workaround Palo Alto API does not like double quotes around boundary so can't use built in .net client
                requestContent.GetType().BaseType?.GetField("_boundary", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(requestContent, boundary);
                var pfxContent = new ByteArrayContent(bytes);
                pfxContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-x509-ca-cert");
                requestContent.Add(pfxContent, "\"file\"", $"\"{name}.pem\"");
                return await GetXmlResponseAsync<ImportCertificateResponse>(
                    await HttpClient.PostAsync(uri, requestContent));
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
                var stringResponse =
                    await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
                var serializer =
                    new XmlSerializer(typeof(T));
                var xmlReader = XmlReader.Create(new StringReader(stringResponse));
                return (T) serializer.Deserialize(xmlReader);
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
                var stringResponse =
                    await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
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