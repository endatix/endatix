using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using MediatR;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public sealed class PartialUpdateFormHandlerBranchTests
{
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IRepository<Theme> _themes = Substitute.For<IRepository<Theme>>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly PartialUpdateFormHandler _handler;

    public PartialUpdateFormHandlerBranchTests() =>
        _handler = new PartialUpdateFormHandler(
            _forms,
            _themes,
            Substitute.For<IRepository<Submission>>(),
            _mediator,
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
    public async Task Handle_FormSettingsChangedDuringTheSave_ReturnsConflictAndPublishesNothing()
    {
        // Another write raised the form's settings version while this save wrote its webhooks.
        var form = StoredForm();
        _forms.UpdateAsync(form, Arg.Any<CancellationToken>())
            .ThrowsAsync(new FormSettingsChangedException("The form settings changed while they were being saved."));

        var result = await _handler.Handle(new PartialUpdateFormCommand(1) { Name = "Renamed" }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        _mediator.ReceivedCalls().Should().BeEmpty("nothing is published for a save that rolled back");
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
