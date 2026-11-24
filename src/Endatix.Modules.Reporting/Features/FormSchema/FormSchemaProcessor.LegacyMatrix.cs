using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.EntityFrameworkCore;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema;

internal sealed partial class FormSchemaProcessor
{
    private sealed record CompilePlan(SchemaRebuild Rebuild, FormSchemaEntity? Existing, FormSchemaCompileMode Mode);

    private async Task<FormSchemaCompileResult> CompileWithHistoryIfNeededAsync(CompilePlan plan, CancellationToken ct)
    {
        var (rebuild, existing, mode) = plan;
        try
        {
            return Compiling(rebuild.Target, () => Compile(rebuild, existing, mode));
        }
        catch (LegacyMatrixSourceRequiredException) when (mode == FormSchemaCompileMode.Merge && existing is not null)
        {
            var history = await HistoricalDefinitionsAsync(existing, ct);
            if (history is null)
            {
                throw;
            }

            PersistedSchemaJson stored = new(existing.FlatteningMap, existing.Codebook);
            return Compiling(rebuild.Target, () => compiler.CompileLegacyMerge(rebuild.Definition.Json, stored, history));
        }
    }

    private async Task<LegacyMatrixHistory?> HistoricalDefinitionsAsync(FormSchemaEntity schema, CancellationToken ct)
    {
        var versions = appDbContext.FormDefinitions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(d => d.TenantId == schema.TenantId && d.FormId == schema.FormId && d.Id <= schema.FormDefinitionRevision);
        // A schema never updated came from just one definition. A merged schema may retain an older question.
        if (schema.ModifiedAt is null)
        {
            versions = versions.Where(d => d.Id == schema.FormDefinitionRevision);
        }

        var sources = await versions.Select(d => new { d.Id, d.ModifiedAt, d.JsonData }).ToListAsync(ct);
        if (!sources.Any(d => d.Id == schema.FormDefinitionRevision) || sources.Any(d => d.ModifiedAt is not null))
        {
            return null;
        }

        return new(FormSchemaFlatteningMap.FromJson(schema.FlatteningMap), [.. sources.Select(d => d.JsonData)]);
    }
}
