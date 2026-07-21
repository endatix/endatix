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
            CreatedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBefore = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
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
    public async Task Validate_WhenCreatedAfterNotBeforeCreatedBefore_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedAfter = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            CreatedBefore = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CreatedAfter");
    }

    [Fact]
    public async Task Validate_WhenCreatedAfterEqualsCreatedBefore_Fails()
    {
        DateTime stamp = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedAfter = stamp,
            CreatedBefore = stamp,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CreatedAfter");
    }

    [Fact]
    public async Task Validate_WhenCompletedAfterNotBeforeCompletedBefore_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletedAfter = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            CompletedBefore = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc),
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CompletedAfter");
    }

    [Fact]
    public async Task Validate_WhenIncompleteWithCompletedAtRange_Fails()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletionStatus = ExportCompletionStatus.Incomplete,
            CompletedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
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
    public async Task Validate_WhenCompletedAfterEqualsCompletedBefore_Fails()
    {
        DateTime stamp = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CompletedAfter = stamp,
            CompletedBefore = stamp,
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor("CompletedAfter");
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
            CompletedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CompletedBefore = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenOnlyCreatedBeforePresent_Passes()
    {
        ExportRequest request = new()
        {
            FormId = 1,
            ExportFormatId = 10,
            CreatedBefore = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
        };

        TestValidationResult<ExportRequest> result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
