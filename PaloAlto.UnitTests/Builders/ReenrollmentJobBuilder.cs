// Copyright 2026 Keyfactor
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

using Keyfactor.Extensions.Orchestrator.PaloAlto;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;

namespace PaloAlto.UnitTests.Builders;

public sealed class ReenrollmentJobBuilder
{
    private string _keyType = "RSA";
    private int _keySize = 1024;
    private string _subjectText = "CN=test.example.com, O=TestOrg, OU=TestOU, L=TestCity, ST=TestState, C=US";

    private string _commonName = "test.example.com";
    private string _organization = "TestOrg";
    private string _organizationUnit = "TestOU";
    private string _locality = "TestCity";
    private string _state = "TestState";
    private string _country = "US";
    
    private string _storePath = "/config/shared";
    private string _clientMachine = "firewall.example.com";
    private string _serverUsername = "admin";
    private string _serverPassword = "password";
    private string _alias = "my-cert";
    private string _deviceGroup = string.Empty;
    private string _templateStack = string.Empty;
    private string? _pushFailureBehavior = "Failure";
    private bool _overwrite = false;
    private Dictionary<string, string[]>? _sans = null;
    
    public ReenrollmentJobBuilder WithClientMachine(string machine) { _clientMachine = machine; return this; }
    public ReenrollmentJobBuilder WithStorePath(string path) { _storePath = path; return this; }
    public ReenrollmentJobBuilder WithDeviceGroup(string group) { _deviceGroup = group; return this; }
    public ReenrollmentJobBuilder WithTemplateStack(string stack) { _templateStack = stack; return this; }
    public ReenrollmentJobBuilder WithCredentials(string username, string password) { _serverUsername = username; _serverPassword = password; return this; }
    public ReenrollmentJobBuilder WithAlias(string alias) { _alias = alias; return this; }
    
    public ReenrollmentJobBuilder WithKeyType(string keyType) { _keyType = keyType; return this; }
    public ReenrollmentJobBuilder WithKeySize(int keySize) { _keySize = keySize; return this; }
    public ReenrollmentJobBuilder WithSubjectText(string subjectText) { _subjectText = subjectText; return this; }
    public ReenrollmentJobBuilder WithOverwrite(bool overwrite) { _overwrite = overwrite; return this; }
    public ReenrollmentJobBuilder WithSans(Dictionary<string, string[]>? sans) { _sans = sans; return this; }
    
    public ReenrollmentJobConfiguration Build() => new()
    {
        JobHistoryId = 1,
        CertificateStoreDetails = new CertificateStore
        {
            ClientMachine = _clientMachine,
            StorePath = _storePath,
            Properties = JsonConvert.SerializeObject(new JobProperties
            {
                DeviceGroup = _deviceGroup,
                TemplateStack = _templateStack,
                InventoryTrustedCerts = false,
                PushFailureBehavior = _pushFailureBehavior,
            }),
        },
        Alias = _alias,
        Overwrite = _overwrite,
        ServerUsername = _serverUsername,
        ServerPassword = _serverPassword,
        JobProperties = new Dictionary<string, object>()
        {
            ["keyType"] = _keyType,
            ["keySize"] = _keySize,
            ["subjectText"] = _subjectText,
        },
        SANs = _sans,
    };
}
