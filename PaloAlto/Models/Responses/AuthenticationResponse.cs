﻿// Copyright 2023 Keyfactor
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
    [XmlRoot(ElementName = "result")]
	public class Result
	{
		[XmlElement(ElementName = "msg")]
		public Msg Msg { get; set; }

		/// <summary>
		/// The Job ID for an asynchronous operation in Palo Alto (commits, software installs, etc.).
		/// Can be used to poll whether a job has completed.
		/// </summary>
		[XmlElement(ElementName = "job")]
		public string JobId { get; set; }

		[XmlElement(ElementName = "key")]
		public string Key { get; set; }
		
		public bool HasJobId => !string.IsNullOrEmpty(JobId);
	}

	[XmlRoot(ElementName = "response")]
	public class AuthenticationResponse
	{

		[XmlElement(ElementName = "result")]
		public Result Result { get; set; }

		[XmlAttribute(AttributeName = "status")]
		public string Status { get; set; }

		[XmlText]
		public string Text { get; set; }
	}
}
