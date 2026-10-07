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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;

public class Reenrollment : JobBase<Reenrollment>, IReenrollmentJobExtension
{
    private IPaloAltoClient _client;
    private JobProperties StoreProperties { get; set; }
    private string ServerUserName { get; set; }
    private string ServerPassword { get; set; }

    public Reenrollment(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public Reenrollment(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory,
        IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }
    
    public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
    {
        throw new NotImplementedException();
    }

}
