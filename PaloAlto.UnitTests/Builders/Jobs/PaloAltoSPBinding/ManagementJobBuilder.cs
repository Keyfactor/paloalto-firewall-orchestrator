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
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;

namespace PaloAlto.UnitTests.Builders.Jobs.PaloAltoSPBinding;

public sealed class ManagementJobBuilder
{
    private string _storePath = "/config/shared";
    private string _clientMachine = "firewall.example.com";
    private string _serverUsername = "admin";
    private string _serverPassword = "password";
    // The alias is the name of the service profile to bind to.
    private string? _alias = "my-profile";
    private string _certContents = string.Empty;
    private string? _contentsFormat = "PEM";
    private string? _thumbprint = null;
    private string? _privateKeyPassword = null;
    private bool _overwrite = false;
    private CertStoreOperationType _operationType = CertStoreOperationType.Add;
    private string _templateStack = string.Empty;
    private string? _pushFailureBehavior = "Failure";
    private readonly Dictionary<string, object> _jobProperties = new Dictionary<string, object>();

    public ManagementJobBuilder AsAdd() { _operationType = CertStoreOperationType.Add; return this; }
    public ManagementJobBuilder AsRemove() { _operationType = CertStoreOperationType.Remove; return this; }
    public ManagementJobBuilder WithOperation(CertStoreOperationType operationType) { _operationType = operationType; return this; }
    public ManagementJobBuilder WithStorePath(string path) { _storePath = path; return this; }
    public ManagementJobBuilder WithAlias(string? alias) { _alias = alias; return this; }
    public ManagementJobBuilder WithCertificateContents(string pfxBase64) { _certContents = pfxBase64; return this; }
    public ManagementJobBuilder WithContentsFormat(string? format) { _contentsFormat = format; return this; }
    public ManagementJobBuilder WithThumbprint(string? thumbprint) { _thumbprint = thumbprint; return this; }
    public ManagementJobBuilder WithPrivateKeyPassword(string password) { _privateKeyPassword = password; return this; }
    public ManagementJobBuilder WithOverwrite(bool overwrite = true) { _overwrite = overwrite; return this; }
    public ManagementJobBuilder WithTemplateStack(string stack) { _templateStack = stack; return this; }
    public ManagementJobBuilder WithCredentials(string username, string password) { _serverUsername = username; _serverPassword = password; return this; }
    public ManagementJobBuilder WithClientMachine(string machine) { _clientMachine = machine; return this; }

    public ManagementJobBuilder WithPushFailureBehavior(string? value) { _pushFailureBehavior = value; return this; }

    public ManagementJobBuilder WithJobProperty(string key, object value) { _jobProperties[key] = value; return this; }

    public ManagementJobConfiguration Build() => new()
    {
        JobHistoryId = 1,
        CertificateStoreDetails = new CertificateStore
        {
            ClientMachine = _clientMachine,
            StorePath = _storePath,
            Properties = JsonConvert.SerializeObject(new JobProperties
            {
                TemplateStack = _templateStack,
                InventoryTrustedCerts = false,
                PushFailureBehavior = _pushFailureBehavior,
            }),
        },
        ServerUsername = _serverUsername,
        ServerPassword = _serverPassword,
        OperationType = _operationType,
        Overwrite = _overwrite,
        JobCertificate = new ManagementJobCertificate
        {
            Alias = _alias,
            Contents = _certContents,
            ContentsFormat = _contentsFormat,
            Thumbprint = _thumbprint,
            PrivateKeyPassword = _privateKeyPassword,
        },
        JobProperties = _jobProperties,
    };
}
