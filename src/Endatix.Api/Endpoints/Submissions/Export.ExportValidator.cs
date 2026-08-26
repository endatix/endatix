using Endatix.Api.Common;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportValidator : Validator<ExportRequest>
{
     public ExportValidator(IExporterFactory exporterFactory)
     {
          var supportedFormats = GetSupportedExportFormats(exporterFactory);

          this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
          this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");
          this.RuleForCalendarDayRange(x => x.StartedFrom, x => x.StartedTo, "StartedFrom");
          this.RuleForCalendarDayRange(x => x.CompletedFrom, x => x.CompletedTo, "CompletedFrom");

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

          RuleFor(x => x.CompletionStatus)
               .IsInEnum()
               .When(x => x.CompletionStatus.HasValue);

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
