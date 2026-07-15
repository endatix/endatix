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
