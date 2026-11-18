using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public sealed class PartialUpdateFormHandlerBranchTests
{
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IRepository<Theme> _themes = Substitute.For<IRepository<Theme>>();
    private readonly PartialUpdateFormHandler _handler;

    public PartialUpdateFormHandlerBranchTests() =>
        _handler = new PartialUpdateFormHandler(
            _forms,
            _themes,
            Substitute.For<IRepository<Submission>>(),
            Substitute.For<IMediator>(),
            FolderAssignmentPolicyStub.Relaxed(SampleData.TENANT_ID));

    [Fact]
    public async Task Handle_UnknownTheme_ReturnsNotFound()
    {
        StoredForm();
        _themes.GetByIdAsync(99L, Arg.Any<CancellationToken>()).Returns((Theme?)null);

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { ThemeId = 99 }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form Theme not found.");
    }

    [Fact]
    public async Task Handle_DefaultThemeId_ClearsTheme()
    {
        var form = StoredForm();
        form.SetTheme(new Theme(SampleData.TENANT_ID, "Theme", "{}") { Id = 4 });

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { ThemeId = 0 }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        form.ThemeId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ClearSubmissionTokenExpiry_ClearsHours()
    {
        var form = StoredForm(12);

        var result = await _handler.Handle(
            new PartialUpdateFormCommand(1) { ClearSubmissionTokenExpiryHours = true },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        form.SubmissionTokenExpiryHours.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PdfUsesFormTheme_SetsTheFlag()
    {
        var form = StoredForm();

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { PdfUsesFormTheme = true }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        form.PdfUsesFormTheme.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PdfUsesFormThemeOmitted_KeepsTheFlag()
    {
        var form = StoredForm();
        form.SetPdfUsesFormTheme(true);

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { Name = "Renamed" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        form.PdfUsesFormTheme.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PdfUsesFormThemeFalse_ClearsTheFlag()
    {
        var form = StoredForm();
        form.SetPdfUsesFormTheme(true);

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { PdfUsesFormTheme = false }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        form.PdfUsesFormTheme.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PdfUsesFormTheme_IsInTheFormUpdatedPayload()
    {
        var form = StoredForm();

        await _handler.Handle(new PartialUpdateFormCommand(1) { PdfUsesFormTheme = true }, CancellationToken.None);

        var payload = form.DomainEvents.OfType<FormUpdatedEvent>().Single().GetPayload();
        System.Text.Json.JsonSerializer.Serialize(payload).Should().Contain("\"pdfUsesFormTheme\":true");
    }

    private Form StoredForm(int? expiryHours = null)
    {
        var form = Form.Create(new FormCreateArgs(
            TenantId: SampleData.TENANT_ID,
            Name: "Test Form",
            SubmissionTokenExpiryHours: expiryHours));
        form.Id = 1;
        _forms.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(form);
        return form;
    }
}
