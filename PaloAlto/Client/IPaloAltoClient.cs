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

using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client;

public interface IPaloAltoClient
{
    Task<CertificateListResponse> GetCertificateList(string path);
    Task<NamedListResponse> GetTemplateList();
    Task<NamedListResponse> GetDeviceGroupList();
    Task<NamedListResponse> GetTemplateStackList();
    Task<CommitResponse> GetCommitResponse();
    Task<CommitResponse> GetCommitAllResponse(string deviceGroup,string storePath,string templateStack);
    Task<TrustedRootListResponse> GetTrustedRootList();
    Task<string> GetCertificateByName(string name);
    Task<ErrorSuccessResponse> SubmitDeleteCertificate(string name, string storePath);
    Task<ErrorSuccessResponse> SubmitDeleteTrustedRoot(string name, string storePath);
    Task<ErrorSuccessResponse> SubmitSetTrustedRoot(string name, string storePath);
    Task<ErrorSuccessResponse> SetPanoramaTarget(string storePath);

    Task<ErrorSuccessResponse> ImportCertificate(string name, string passPhrase, byte[] bytes,
        string includeKey, string category, string storePath);
}
