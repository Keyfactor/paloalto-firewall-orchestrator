using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Moq;

namespace PaloAlto.UnitTests.Fakes;

public sealed class FakePaloAltoClient
{
    public readonly Mock<IPaloAltoClient> ClientMock = new ();
    
    public void WithCertificates(params Entry[] entries) =>
        ClientMock.Setup(c => c.GetCertificateList(It.IsAny<string>()))
            .ReturnsAsync(new CertificateListResponse
            {
                CertificateResult = new CertificateResult { Entry = entries.ToList() }
            });

    public void WithNoCertificates() => WithCertificates();

    public void WithTrustedRoots(params TrustedRootEntry[] entries) =>
        ClientMock.Setup(c => c.GetTrustedRootList())
            .ReturnsAsync(new TrustedRootListResponse
            {
                TrustedRootResult = new TrustedRootResult
                {
                    TrustedRootCa = new TrustedRootCa { Entry = entries.ToList() }
                }
            });

    public void WithNoTrustedRoots() => WithTrustedRoots();

    public void WithTrustedRootPemAvailable(string name, string testPem) =>
        ClientMock.Setup(c => c.GetCertificateByName(name)).ReturnsAsync(testPem);

    public void WithTrustedRootPemUnavailable(string name) =>
        ClientMock.Setup(c => c.GetCertificateByName(name))
            .ThrowsAsync(new Exception($"Fetch failed for '{name}'"));

    public void PanoramaHasTemplate(string templateName) =>
        ClientMock.Setup(c => c.GetTemplateList())
            .ReturnsAsync(new NamedListResponse
            {
                Result = new NamedListResult
                {
                    Entry = new List<NamedListEntry> { new() { Name = templateName } }
                }
            });

    public void PanoramaHasDeviceGroups(params string[] names) =>
        ClientMock.Setup(c => c.GetDeviceGroupList())
            .ReturnsAsync(new NamedListResponse
            {
                Result = new NamedListResult
                {
                    Entry = names.Select(n => new NamedListEntry { Name = n }).ToList()
                }
            });

    public void PanoramaHasTemplateStacks(params string[] names) =>
        ClientMock.Setup(c => c.GetTemplateStackList())
            .ReturnsAsync(new NamedListResponse
            {
                Result = new NamedListResult
                {
                    Entry = names.Select(n => new NamedListEntry { Name = n }).ToList()
                }
            });
}
