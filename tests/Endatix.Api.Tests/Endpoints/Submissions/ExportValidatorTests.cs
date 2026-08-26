using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class ExportValidatorTests
{
    private readonly ExportValidator _validator;

    public ExportValidatorTests()
    {
        IExporterFactory exporterFactory = Substitute.For<IExporterFactory>();
        exporterFactory.GetSupportedFormats<SubmissionExportRow>().Returns(["csv", "json"]);
        exporterFactory.GetSupportedFormats<DynamicExportRow>().Returns(["codebook", "codebook-shoji"]);
        _validator = new ExportValidator(exporterFactory);
    }

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            Locale = "es",
            CreatedFrom = "2026-01-01",
            CreatedTo = "2026-01-02",
            ModifiedFrom = "2026-01-01",
            ModifiedTo = "2026-01-02",
            MinSubmissionId = 1,
            MaxSubmissionId = 10,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenFormIdMissing_Fails()
    {
        ExportRequest request = new() { FormId = 0, ExportFormatId = 10 };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.FormId);
    }

    [Fact]
    public async Task Validate_WhenLocaleTooLong_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            Locale = new string('x', 33),
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Locale);
    }

    [Fact]
    public async Task Validate_WhenCreatedFromAfterCreatedTo_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedFrom = "2026-01-03",
            CreatedTo = "2026-01-02",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CreatedFrom");
    }

    [Fact]
    public async Task Validate_WhenCreatedFromEqualsCreatedTo_Passes()
    {
        // Same calendar day is valid: InclusiveStart < ExclusiveEnd (next day).
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedFrom = "2026-01-02",
            CreatedTo = "2026-01-02",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenModifiedFromAfterModifiedTo_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            ModifiedFrom = "2026-01-03",
            ModifiedTo = "2026-01-02",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("ModifiedFrom");
    }

    [Fact]
    public async Task Validate_WhenStartedFromAfterStartedTo_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            StartedFrom = "2026-01-03",
            StartedTo = "2026-01-02",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("StartedFrom");
    }

    [Fact]
    public async Task Validate_WhenCompletedFromAfterCompletedTo_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletedFrom = "2026-01-05",
            CompletedTo = "2026-01-04",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CompletedFrom");
    }

    [Fact]
    public async Task Validate_WhenIncompleteWithCompletedAtRange_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletionStatus = ExportCompletionStatus.Incomplete,
            CompletedFrom = "2026-01-01",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CompletionStatus");
    }

    [Fact]
    public async Task Validate_WhenMinSubmissionIdGreaterThanMax_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            MinSubmissionId = 20,
            MaxSubmissionId = 10,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("MinSubmissionId");
    }

    [Fact]
    public async Task Validate_WhenSubmissionIdNotPositive_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            MinSubmissionId = 0,
            MaxSubmissionId = -1,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.MinSubmissionId);
        result.ShouldHaveValidationErrorFor(x => x.MaxSubmissionId);
    }

    [Fact]
    public async Task Validate_WhenExportFormatUnsupported_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormat = "xlsx",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.ExportFormat);
    }

    [Fact]
    public async Task Validate_WhenCompletedFromEqualsCompletedTo_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletedFrom = "2026-01-02",
            CompletedTo = "2026-01-02",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenMinSubmissionIdEqualsMax_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            MinSubmissionId = 42,
            MaxSubmissionId = 42,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenIncompleteWithoutCompletedAtRange_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletionStatus = ExportCompletionStatus.Incomplete,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenCompletedWithCompletedAtRange_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletionStatus = ExportCompletionStatus.Completed,
            CompletedFrom = "2026-01-01",
            CompletedTo = "2026-01-05",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenOnlyCreatedToPresent_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedTo = "2026-01-03",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenCreatedFromInvalidCalendarDay_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedFrom = "01-01-2026",
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.CreatedFrom);
    }
}
