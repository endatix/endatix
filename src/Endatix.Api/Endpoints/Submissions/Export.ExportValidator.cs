using Endatix.Api.Common;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportValidator : Validator<ExportRequest>
{
     /// <summary>Declares the rules. It takes no services: FastEndpoints builds it once, from the root provider.</summary>
     public ExportValidator()
     {
          Include(new CreatedRangeValidator());
          Include(new ModifiedRangeValidator());
          Include(new StartedRangeValidator());
          Include(new CompletedRangeValidator());

          RuleFor(x => x.FormId)
               .GreaterThan(0);

          RuleFor(x => x.ExportId)
               .GreaterThan(0)
               .When(x => x.ExportId.HasValue);

          RuleFor(x => x.ExportFormatId)
               .GreaterThan(0)
               .When(x => x.ExportFormatId.HasValue);

          RuleFor(x => x.ExportFormat)
               .Must(format => SupportedExportFormats().Contains(format, StringComparer.OrdinalIgnoreCase))
               .WithMessage(_ => $"Export format not supported. Supported formats: {string.Join(", ", SupportedExportFormats())}")
               .When(x => x.ExportFormat is not null);

          RuleFor(x => x.Locale)
               .MaximumLength(32)
               .When(x => x.Locale is not null);

          RuleFor(x => x.CompletionStatus)
               .IsInEnum()
               .When(x => x.CompletionStatus.HasValue);

          // A malformed filter must fail, not fall back to completionStatus and export more rows.
          RuleFor(x => x.CollectionStatus)
               .Must(HaveValidCollectionStatusCodes)
               .WithMessage(
                    $"CollectionStatus must be one or more pipe-separated codes of at most {Endatix.Core.Entities.CollectionStatus.CODE_MAX_LENGTH} characters.")
               .When(x => x.CollectionStatus is not null);

          RuleFor(x => x)
               .Must(request => request.CompletionStatus is not ExportCompletionStatus.Incomplete ||
                                (string.IsNullOrWhiteSpace(request.CompletedFrom) &&
                                 string.IsNullOrWhiteSpace(request.CompletedTo)))
               .WithMessage("CompletedFrom/CompletedTo cannot be used when CompletionStatus is incomplete.")
               .WithName("CompletionStatus");

          RuleFor(x => x)
               .Must(request => !request.MinSubmissionId.HasValue ||
                                !request.MaxSubmissionId.HasValue ||
                                request.MinSubmissionId <= request.MaxSubmissionId)
               .WithMessage("MinSubmissionId must be less than or equal to MaxSubmissionId.")
               .WithName("MinSubmissionId");

          RuleFor(x => x.MinSubmissionId)
               .GreaterThan(0)
               .When(x => x.MinSubmissionId.HasValue);

          RuleFor(x => x.MaxSubmissionId)
               .GreaterThan(0)
               .When(x => x.MaxSubmissionId.HasValue);
     }

     // FastEndpoints builds a validator once, from the root provider, and IExporterFactory is scoped: resolve it in
     // the request's scope when a rule runs, never in the constructor (#1712).
     private IReadOnlyList<string> SupportedExportFormats() => GetSupportedExportFormats(Resolve<IExporterFactory>());

     private static IReadOnlyList<string> GetSupportedExportFormats(IExporterFactory exporterFactory)
     {
          var submissionFormats = exporterFactory.GetSupportedFormats<SubmissionExportRow>();
          var dynamicFormats = exporterFactory.GetSupportedFormats<DynamicExportRow>();

          return submissionFormats
               .Concat(dynamicFormats)
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToList()
               .AsReadOnly();
     }

     private static bool HaveValidCollectionStatusCodes(string? raw)
     {
          var codes = (raw ?? string.Empty).Split(
               '|',
               StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
          return codes.Length > 0 &&
                 codes.All(code => code.Length <= Endatix.Core.Entities.CollectionStatus.CODE_MAX_LENGTH);
     }
}
