using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Data;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Crunch.io Shoji codebook export — projects neutral FormSchema artifacts to Shoji JSON.
/// </summary>
internal sealed class ShojiCodebookExportDataSource(IFormSchemaRepository formSchemaRepository) : IExportDataSource
{
    public bool Matches(ExportDataSourceRequest request) =>
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        request.ItemType == typeof(DynamicExportRow) &&
        request.Format.Equals("codebook", StringComparison.OrdinalIgnoreCase);

    public Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(context.Options));

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(
            context.TenantId,
            context.FormId,
            cancellationToken);
        if (schema is null)
        {
            throw new InvalidOperationException(ReportingExportSchemaHelper.MissingSchemaMessage);
        }

        if (!ReportingExportSchemaHelper.HasValidSchemaArtifacts(schema))
        {
            throw new InvalidOperationException(ReportingExportSchemaHelper.InvalidSchemaArtifactsMessage);
        }

        var codebookJson = ShojiCodebookGenerator.Generate(schema.FlatteningMap, schema.Codebook);
        yield return new DynamicExportRow { Data = codebookJson };
    }
}
