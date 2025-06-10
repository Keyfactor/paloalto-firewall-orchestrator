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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client
{
    public class PaloAltoClient : IPaloAltoClient
    {
        private readonly ILogger _logger;

        public PaloAltoClient(string url, string userName, string password)
        {
            _logger = LogHandler.GetClassLogger<PaloAltoClient>();
            ServerUserName = Uri.EscapeDataString(userName);
            ServerPassword = Uri.EscapeDataString(password);
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

        public async Task<NamedListResponse> GetTemplateStackList()
        {
            try
            {
                var uri =
                    $"/api/?type=config&action=get&xpath=/config/devices/entry[@name='localhost.localdomain']/template-stack/entry/@name&key={ApiKey}";
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

        public async Task<CommitResponse> GetCommitAllResponse(string deviceGroup,string storePath,string templateStack)
        {
            try
            {
                //Palo alto claims this commented out line works for push to devices by userid but can't get this to work
                //var uri = $"/api/?&type=commit&action=all&cmd=<commit-all><shared-policy><admin><member>{ServerUserName}</member></admin><device-group><entry name=\"{deviceGroup}\"/></device-group></shared-policy></commit-all>&key={ApiKey}";
                var uri = string.Empty;
                if (!String.IsNullOrEmpty(deviceGroup))
                {
                     uri =
                        $"/api/?&type=commit&action=all&cmd=<commit-all><shared-policy><device-group><entry name=\"{deviceGroup}\"/></device-group></shared-policy></commit-all>&key={ApiKey}";
                }
                else
                {
                    uri =$"/api/?&type=commit&action=all&cmd=<commit-all><template><name>{GetTemplateName(storePath)}</name></template></commit-all>&key={ApiKey}";
                }

                var response = await GetXmlResponseAsync<CommitResponse>(await HttpClient.GetAsync(uri));

                if (!String.IsNullOrEmpty(templateStack))
                {
                    uri = $"/api/?&type=commit&action=all&cmd=<commit-all><template-stack><name>{templateStack}</name></template-stack></commit-all>&key={ApiKey}";
                    Thread.Sleep(60000); //Some delay built in so pushes to devices work
                    response = await GetXmlResponseAsync<CommitResponse>(await HttpClient.GetAsync(uri));
                }
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.GetCertificateList: {e.Message}");
                throw;
            }
        }


        public string GetTemplateName(string storePath)
        {
            string pattern = @"\/template\/entry\[@name='([^']+)'\]";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(storePath);

            string templateName = string.Empty;
            if (match.Success)
            {
                templateName = match.Groups[1].Value;
            }

            return templateName;
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


        public async Task<ErrorSuccessResponse> SubmitDeleteCertificate(string name, string storePath)
        {
            try
            {
                string uri =$@"/api/?type=config&action=delete&xpath={storePath}/certificate/entry[@name='{name}']&key={ApiKey}&target-tpl={GetTemplateName(storePath)}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteCertificate: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SubmitDeleteTrustedRoot(string name, string storePath)
        {
            try
            {
                string uri= $@"/api/?type=config&action=delete&xpath={storePath}/ssl-decrypt/trusted-root-CA/member[text()='{name}']&key={ApiKey}&target-tpl={GetTemplateName(storePath)}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitDeleteTrustedRoot: {e.Message}");
                throw;
            }
        }


        public async Task<ErrorSuccessResponse> SubmitSetTrustedRoot(string name, string storePath)
        {
            try
            {
                string uri = $@"/api/?type=config&action=set&xpath={storePath}/ssl-decrypt&element=<trusted-root-CA><member>{name}</member></trusted-root-CA>&key={ApiKey}&target-tpl={GetTemplateName(storePath)}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitSetTrustedRoot: {e.Message}");
                throw;
            }
        }

        public async Task<ErrorSuccessResponse> SetPanoramaTarget(string storePath)
        {
            try
            {
                string uri = $"/api/?type=op&cmd=<set><system><setting><target><template><name>{GetTemplateName(storePath)}</name><vsys>{GetVirtualSystemFromPath(storePath)}</vsys></template></target></setting></system></set>&key={ApiKey}";
                return await GetXmlResponseAsync<ErrorSuccessResponse>(await HttpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.SubmitSetTrustedRoot: {e.Message}");
                throw;
            }
        }


        public async Task<ErrorSuccessResponse> ImportCertificate(string name, string passPhrase, byte[] bytes,
            string includeKey, string category, string storePath)
        {
            try
            {
                var templateName=GetTemplateName(storePath);
                var vsys = GetVirtualSystemFromPath(storePath);
                string uri;
                if (!Validators.IsValidPanoramaVsysFormat(storePath))
                {
                    uri =$@"/api/?type=import&category={category}&certificate-name={name}&format=pem&include-key={includeKey}&passphrase={passPhrase}&target-tpl={templateName}&vsys={vsys}&key={ApiKey}";
                }
                else
                {
                    uri = $@"/api/?type=import&category={category}&certificate-name={name}&format=pem&include-key={includeKey}&passphrase={passPhrase}&key={ApiKey}";
                }

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
                return await GetXmlResponseAsync<ErrorSuccessResponse>(
                    await HttpClient.PostAsync(uri, requestContent));
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occured in PaloAltoClient.ImportCertificate: {e.Message}");
                throw;
            }
        }

        static string GetVirtualSystemFromPath(string path)
        {
            string pattern = @"vsys/entry\[@name='([^']*)'\]";

            Match match = Regex.Match(path, pattern);

            if (match.Success)
            {
                string vsysName = match.Groups[1].Value;
                return vsysName;
            }

            return "";
        }
        public async Task<T> GetXmlResponseAsync<T>(HttpResponseMessage response)
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

        public async Task<string> GetResponseAsync(HttpResponseMessage response)
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

        public string MaskSensitiveData(string json)
        {
            try
            {
                JObject jsonObject = JObject.Parse(json);

                // Replace all keys named "Password" or similar
                MaskKey(jsonObject, "StorePassword");
                MaskKey(jsonObject, "ServerPassword");
                MaskKey(jsonObject, "PrivateKeyPassword");

                return jsonObject.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Invalid JSON provided: " + ex.Message);
                return json; // Return the original JSON if parsing fails
            }
        }

        private static void MaskKey(JObject jsonObject, string key)
        {
            foreach (var property in jsonObject.Properties())
            {
                if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = "*****";
                }
                else if (property.Value.Type == JTokenType.Object)
                {
                    MaskKey((JObject)property.Value, key);
                }
                else if (property.Value.Type == JTokenType.String)
                {
                    // Optionally handle nested JSON strings
                    string value = property.Value.ToString();
                    if (value.StartsWith("{") && value.EndsWith("}"))
                    {
                        try
                        {
                            JObject nestedObject = JObject.Parse(value);
                            MaskKey(nestedObject, key);
                            property.Value = nestedObject.ToString(Newtonsoft.Json.Formatting.None);
                        }
                        catch
                        {
                            // Not a valid JSON string, skip
                        }
                    }
                }
            }
        }
    }
}