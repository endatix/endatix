using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using MediatR;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormWebhookTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_RetiredWebhookWithFolderMove_ThrowsBeforeFolderPolicyAndLeavesFormUnchanged(bool destinationIsActive)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var form = Form.Create(11, new FormCreateArgs(1, "Original", FolderId: 7,
            WebHookSettingsJson: """{"Events":{"SubmissionClosed":{}}}"""));
        form.SetTheme(new Theme(1, "Original theme") { Id = 3 });
        var before = StateOf(form);
        var forms = Substitute.For<IRepository<Form>>();
        forms.GetByIdAsync(form.Id, ct).Returns(form);
        var folders = Substitute.For<IRepository<Folder>>();
        folders.FirstOrDefaultAsync(Arg.Any<ISpecification<Folder, bool?>>(), ct).Returns(destinationIsActive);
        var policy = new FolderAssignmentPolicy(Substitute.For<IRepository<TenantSettingsEntity>>(), folders, SampleData.Tenant);
        var handler = new PartialUpdateFormHandler(forms, Substitute.For<IRepository<Theme>>(),
            Substitute.For<IRepository<Submission>>(), Substitute.For<IMediator>(), policy);
        var request = MoveWithRetiredWebhook(form.Id);

        // Act
        var error = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(request, ct));

        // Assert
        Assert.Equal("settings", error.ParamName);
        Assert.Equal("The webhook setting 'SubmissionCollectionStatusChanged' was renamed to 'SubmissionClosed'. " +
            "Use an Endatix Hub release that writes 'SubmissionClosed'.", error.EndUserMessage);
        Assert.Equal(before, StateOf(form));
        Assert.Empty(form.DomainEvents);
        Assert.Empty(folders.ReceivedCalls());
        await forms.DidNotReceive().UpdateAsync(Arg.Any<Form>(), Arg.Any<CancellationToken>());
        await forms.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static PartialUpdateFormCommand MoveWithRetiredWebhook(long id) => new(id)
    {
        Name = "Changed",
        IsEnabled = true,
        ThemeId = 0,
        FolderId = 8,
        WebHookSettingsJson = """{"Events":{"SubmissionCollectionStatusChanged":{}}}"""
    };

    private static (string, bool, long?, long?, string?, long) StateOf(Form form) =>
        (form.Name, form.IsEnabled, form.FolderId, form.ThemeId, form.WebHookSettingsJson, form.Revision);
}
