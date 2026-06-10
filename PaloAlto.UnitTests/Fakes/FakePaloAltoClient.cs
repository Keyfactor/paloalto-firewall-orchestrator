using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Moq;

namespace PaloAlto.UnitTests.Fakes;

public sealed class FakePaloAltoClient
{
    public readonly Mock<IPaloAltoClient> ClientMock = new();

    // ── Inventory: Certificate list ──────────────────────────────────────────

    public void WithCertificates(params Entry[] entries) =>
        ClientMock.Setup(c => c.GetCertificateList(It.IsAny<string>()))
            .ReturnsAsync(new CertificateListResponse
            {
                CertificateResult = new CertificateResult { Entry = entries.ToList() }
            });

    public void WithNoCertificates() => WithCertificates();

    // ── Inventory: Trusted roots ─────────────────────────────────────────────

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

    // ── Validation: Panorama topology ────────────────────────────────────────

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

    // ── Management: Panorama target ──────────────────────────────────────────

    public void SetPanoramaTargetSucceeds() =>
        ClientMock.Setup(c => c.SetPanoramaTarget(It.IsAny<string>()))
            .ReturnsAsync(Ok());

    public void SetPanoramaTargetFails(string message = "Could not set Panorama target") =>
        ClientMock.Setup(c => c.SetPanoramaTarget(It.IsAny<string>()))
            .ReturnsAsync(Error(message));

    // ── Management: Duplicate check ──────────────────────────────────────────

    // Returns a list with one entry that has a PublicKey, making CheckForDuplicate return true.
    public void DuplicateExists(string alias) =>
        WithCertificates(new Entry { Name = alias, PublicKey = "existing-pem" });

    public void NoDuplicateExists() => WithNoCertificates();

    // ── Management: Import ───────────────────────────────────────────────────

    public void ImportSucceeds() =>
        ClientMock.Setup(c => c.ImportCertificate(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Ok());

    public void ImportFails(string message = "Import failed") =>
        ClientMock.Setup(c => c.ImportCertificate(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Error(message));

    // ── Management: Delete certificate ───────────────────────────────────────

    public void DeleteCertificateSucceeds() =>
        ClientMock.Setup(c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Ok());

    public void DeleteCertificateFails(string message) =>
        ClientMock.Setup(c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Error(message));

    // First call returns a trusted-root-CA error; subsequent calls succeed.
    // Used to test the three-step trusted root removal dance.
    public void DeleteCertificateFailsThenSucceeds(string trustedRootErrorMessage) =>
        ClientMock.SetupSequence(c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Error(trustedRootErrorMessage))
            .ReturnsAsync(Ok());

    public void DeleteCertificateAlwaysFails(string message) =>
        ClientMock.SetupSequence(c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Error(message))
            .ReturnsAsync(Error(message));

    // ── Management: Delete trusted root ─────────────────────────────────────

    public void DeleteTrustedRootSucceeds() =>
        ClientMock.Setup(c => c.SubmitDeleteTrustedRoot(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Ok());

    public void DeleteTrustedRootFails(string message = "Cannot delete trusted root") =>
        ClientMock.Setup(c => c.SubmitDeleteTrustedRoot(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Error(message));

    // ── Management: Commit ───────────────────────────────────────────────────

    public void CommitSucceeds() =>
        ClientMock.Setup(c => c.GetCommitResponse())
            .ReturnsAsync(new CommitResponse { Status = "success" });

    public void CommitSucceedsWithJobId(string jobId) =>
        ClientMock.Setup(c => c.GetCommitResponse())
            .ReturnsAsync(new CommitResponse { Status = "success", Result = new Result { JobId = jobId } });

    public void CommitFails(string text = "commit failed") =>
        ClientMock.Setup(c => c.GetCommitResponse())
            .ReturnsAsync(new CommitResponse { Status = "error", Text = text });

    public void CommitAllSucceeds() =>
        ClientMock.Setup(c => c.GetCommitAllResponse(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CommitResponse { Status = "success" });

    public void CommitAllFails(string text = "push to devices failed") =>
        ClientMock.Setup(c => c.GetCommitAllResponse(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CommitResponse { Status = "error", Text = text });

    public void CommitDeviceGroupSucceeds() => ClientMock
        .Setup(c => c.CommitDeviceGroup(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = true, Message = "success" });
    
    public void CommitDeviceGroupFails(string text = "push to device group failed") => ClientMock
        .Setup(c => c.CommitDeviceGroup(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = false, Message = text });
    
    public void CommitTemplateSucceeds() => ClientMock
        .Setup(c => c.CommitTemplate(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = true, Message = "success" });
    
    public void CommitTemplateFails(string text = "push to template failed") => ClientMock
        .Setup(c => c.CommitTemplate(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = false, Message = text });
    
    public void CommitTemplateStackSucceeds() => ClientMock
        .Setup(c => c.CommitTemplateStack(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = true, Message = "success" });
    
    public void CommitTemplateStackFails(string text = "push to template stack failed") => ClientMock
        .Setup(c => c.CommitTemplateStack(It.IsAny<string>())).ReturnsAsync(new CommitResponseResult()
            { IsSuccess = false, Message = text });

    // ── Management: Job polling ──────────────────────────────────────────────

    public void JobCompletesSuccessfully(string jobId) =>
        ClientMock.Setup(c => c.GetJobStatus(jobId))
            .ReturnsAsync(new JobStatusResponse
            {
                Result = new JobStatusResult { Job = new Job { Status = "FIN", Result = "OK" } }
            });

    public void JobFails(string jobId) =>
        ClientMock.Setup(c => c.GetJobStatus(jobId))
            .ReturnsAsync(new JobStatusResponse
            {
                Result = new JobStatusResult
                {
                    Job = new Job { Status = "FIN", Result = "FAIL", Details = new Msg { Line = new List<string>() } }
                }
            });

    // ── Private helpers ──────────────────────────────────────────────────────

    private static ErrorSuccessResponse Ok() =>
        new() { Status = "ok", LineMsg = new Msg { Line = new List<string>() } };

    private static ErrorSuccessResponse Error(string message) =>
        new() { Status = "error", LineMsg = new Msg { Line = new List<string> { message } } };
}
