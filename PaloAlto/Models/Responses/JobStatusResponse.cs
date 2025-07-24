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

using System.Xml.Serialization;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses
{
    [XmlRoot(ElementName = "response")]
    public class JobStatusResponse
    {
        [XmlElement(ElementName = "result")]
        public JobStatusResult Result { get; set; }
    }

    public class JobStatusResult
    {
        [XmlElement(ElementName = "job")]
        public Job Job { get; set; }
    }

    public class Job
    {
        [XmlElement(ElementName = "progress")]
        public string Progress { get; set; }
        
        [XmlElement(ElementName = "result")]
        public string Result { get; set; }
        
        [XmlElement(ElementName = "status")]
        public string Status { get; set; }
        
        [XmlElement(ElementName = "details")]
        public Msg Details { get; set; }
    }
}

