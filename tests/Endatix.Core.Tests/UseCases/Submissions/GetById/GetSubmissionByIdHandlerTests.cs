using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.GetById;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.UseCases.Submissions.GetById;

public class GetSubmissionByIdHandlerTests
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly IRepository<Theme> _themeRepository = Substitute.For<IRepository<Theme>>();
    private readonly IFormSettingsRepository _formSettings = Substitute.For<IFormSettingsRepository>();
    private readonly ILogger<SubmissionFormTheme> _logger = Substitute.For<ILogger<SubmissionFormTheme>>();
    private readonly GetByIdHandler _handler;

    public GetSubmissionByIdHandlerTests()
    {
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _handler = new GetByIdHandler(_submissionsRepository, new SubmissionFormTheme(_formSettings, _themeRepository, _logger));
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
    public async Task Handle_PdfSettingsUseTheTheme_LoadsTheFormTheme()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Theme", jsonData: "{}") { Id = 7 };
        var submission = SubmissionOfFormWithTheme(theme.Id);
        PdfSettingsOf(submission.Form, usesFormTheme: true);
        _themeRepository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Form.Theme.Should().BeSameAs(theme);
    }

    [Fact]
    public async Task Handle_PdfSettingsDontUseTheTheme_DoesNotReadTheTheme()
    {
        // Arrange
        var submission = SubmissionOfFormWithTheme(themeId: 7);
        PdfSettingsOf(submission.Form, usesFormTheme: false);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _themeRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default(long), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FormWithoutATheme_DoesNotReadTheSettings()
    {
        // Arrange
        var submission = SubmissionOfFormWithTheme(themeId: null);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _formSettings.DidNotReceiveWithAnyArgs().ReadAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_UnreadableStoredPdfSettings_LogsAWarningAndLoadsNoTheme()
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Warning).Returns(true);
        var submission = SubmissionOfFormWithTheme(themeId: 7);
        var settings = Core.Entities.FormSettings.For(submission.Form);
        typeof(Core.Entities.FormSettings).GetProperty(nameof(Core.Entities.FormSettings.PdfJson))!.SetValue(settings, "[]");
        _formSettings.ReadAsync(submission.Form, Arg.Any<CancellationToken>()).Returns(settings);

        // Act
        var result = await _handler.Handle(new GetByIdQuery(1, 1), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Form.Theme.Should().BeNull();
        await _themeRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default(long), TestContext.Current.CancellationToken);
        var logCall = _logger.ReceivedCalls().Should().ContainSingle(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Subject.GetArguments();
        logCall[0].Should().Be(LogLevel.Warning);
        var message = logCall[2]!.ToString();
        message.Should().Contain(submission.Form.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).And.Contain("pdf");
        message.Should().NotContain("[]");
    }

    private void PdfSettingsOf(Form form, bool usesFormTheme)
    {
        var settings = Core.Entities.FormSettings.For(form);
        settings.Apply(new FormSettingsChange { Pdf = new PdfSettings { UsesFormTheme = usesFormTheme } }, form, 0, DateTime.UtcNow);
        _formSettings.ReadAsync(form, Arg.Any<CancellationToken>()).Returns(settings);
    }

    private Submission SubmissionOfFormWithTheme(long? themeId)
    {
        var form = Form.Create(1, new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form", IsEnabled: true));
        if (themeId is { } id)
        {
            form.SetTheme(new Theme(SampleData.TENANT_ID, "Stored") { Id = id });
            typeof(Form).GetProperty(nameof(Form.Theme))!.SetValue(form, null); // the submission query loads the form without its theme
        }

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID, FormId: 1, FormDefinitionId: 1, JsonData: "{}"));
        typeof(Submission).GetProperty(nameof(Submission.Form))!.SetValue(submission, form);
        _submissionsRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
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
