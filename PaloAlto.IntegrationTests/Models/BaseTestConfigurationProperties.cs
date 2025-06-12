// Copyright 2025 Keyfactor
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

namespace PaloAlto.IntegrationTests.Models;

public abstract class BaseTestConfigurationProperties
{
    public string ServerUsername { get; set; }
    public string ServerPassword { get; set; }
    public string MachineName { get; set; }
    
    public string StorePath { get; set; }
    public string TemplateStack { get; set; }
    public string DeviceGroup { get; set; }
    
    public void AddFirewallCredentials()
    {
        ServerUsername = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_USER");
        ServerPassword = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_PASSWORD");
        MachineName = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_HOST");
    }

    public void AddPanoramaCredentials()
    {
        ServerUsername = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_USER");
        ServerPassword = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_PASSWORD");
        MachineName = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_HOST");
    }
}
