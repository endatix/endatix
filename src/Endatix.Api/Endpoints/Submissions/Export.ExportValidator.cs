using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportValidator : Validator<ExportRequest>
{
     public ExportValidator(IExporterFactory exporterFactory)
     {
          var supportedFormats = exporterFactory.GetSupportedFormats<SubmissionExportRow>();

          RuleFor(x => x.FormId)
               .GreaterThan(0);

          RuleFor(x => x.ExportId)
               .GreaterThan(0)
               .When(x => x.ExportId.HasValue);

          RuleFor(x => x.ExportFormat)
               .Must(format => supportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
               .WithMessage($"Export format not supported. Supported formats: {string.Join(", ", supportedFormats)}")
               .When(x => x.ExportFormat is not null);
     }
}
