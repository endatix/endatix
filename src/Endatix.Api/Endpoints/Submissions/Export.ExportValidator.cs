using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportValidator : Validator<ExportRequest>
{
     public ExportValidator(IExporterFactory exporterFactory)
     {
          var supportedFormats = GetSupportedExportFormats(exporterFactory);

          RuleFor(x => x.FormId)
               .GreaterThan(0);

          RuleFor(x => x.ExportId)
               .GreaterThan(0)
               .When(x => x.ExportId.HasValue);

          RuleFor(x => x.ExportFormatId)
               .GreaterThan(0)
               .When(x => x.ExportFormatId.HasValue);

          RuleFor(x => x.ExportFormat)
               .Must(format => supportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
               .WithMessage($"Export format not supported. Supported formats: {string.Join(", ", supportedFormats)}")
               .When(x => x.ExportFormat is not null);

          RuleFor(x => x.Locale)
               .MaximumLength(32)
               .When(x => x.Locale is not null);

          // CreatedBefore / CompletedBefore are exclusive upper bounds in the reporting repository.
          RuleFor(x => x)
               .Must(request => !request.CreatedAfter.HasValue ||
                                !request.CreatedBefore.HasValue ||
                                request.CreatedAfter < request.CreatedBefore)
               .WithMessage("CreatedAfter must be earlier than CreatedBefore (exclusive upper bound).")
               .WithName("CreatedAfter");

          RuleFor(x => x)
               .Must(request => !request.CompletedAfter.HasValue ||
                                !request.CompletedBefore.HasValue ||
                                request.CompletedAfter < request.CompletedBefore)
               .WithMessage("CompletedAfter must be earlier than CompletedBefore (exclusive upper bound).")
               .WithName("CompletedAfter");

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
}
