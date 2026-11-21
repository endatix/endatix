using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.GetById;

namespace Endatix.Core.Tests.UseCases.Submissions.GetById;

public class GetSubmissionByIdHandlerTests
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly IRepository<Theme> _themeRepository = Substitute.For<IRepository<Theme>>();
    private readonly GetByIdHandler _handler;

    public GetSubmissionByIdHandlerTests()
    {
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _handler = new GetByIdHandler(_submissionsRepository, _themeRepository);
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetByIdQuery(1, 1);
        _submissionsRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");
    }

    [Fact]
    public async Task Handle_PdfUsesFormTheme_LoadsTheFormTheme()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Theme", jsonData: "{}") { Id = 7 };
        var submission = SubmissionOfFormWithTheme(theme.Id, pdfUsesFormTheme: true);
        _submissionsRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _themeRepository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), CancellationToken.None);

        // Assert
        result.Value.Form.Theme.Should().BeSameAs(theme);
    }

    [Fact]
    public async Task Handle_PdfUsesFormThemeOff_DoesNotReadTheTheme()
    {
        // Arrange
        var submission = SubmissionOfFormWithTheme(themeId: 7, pdfUsesFormTheme: false);
        _submissionsRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _themeRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default(long), TestContext.Current.CancellationToken);
    }

    private static Submission SubmissionOfFormWithTheme(long themeId, bool pdfUsesFormTheme)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form", IsEnabled: true));
        form.SetTheme(new Theme(SampleData.TENANT_ID, "Stored") { Id = themeId });
        form.SetPdfUsesFormTheme(pdfUsesFormTheme);
        var unloaded = typeof(Form).GetProperty(nameof(Form.Theme))!;
        unloaded.SetValue(form, null); // the submission query loads the form without its theme
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID, FormId: 1, FormDefinitionId: 1, JsonData: "{}"));
        typeof(Submission).GetProperty(nameof(Submission.Form))!.SetValue(submission, form);
        return submission;
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSubmission()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 1L;
        var submissionId = 1L;

        var form = Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form", IsEnabled: true));
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = Submission.Create(submissionId, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: "{ }"));

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        var request = new GetByIdQuery(formId, submissionId);

        _submissionsRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(submission);
    }
}
