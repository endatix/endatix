using Endatix.Core.Abstractions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// A form's search copy as a schema compile sees it: the keys the compiled schema keeps, and the rebuild of the rows
/// when those keys change what the rows should hold.
/// </summary>
internal sealed class FormSearchCopy(IDateTimeProvider dateTimeProvider, SearchCopyRebuilder rebuilder)
{
    /// <summary>The search keys of <paramref name="existing"/> after <paramref name="compiled"/> is saved over it.</summary>
    public SearchKeysRefresh KeysAfter(FormSchemaEntity? existing, FormSchemaCompileResult compiled)
    {
        IReadOnlySet<string> previousColumns = existing is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : ColumnKeys(FormSchemaFlatteningMap.FromJson(existing.FlatteningMap));
        SearchKeysCompile compile = new(compiled.SearchableColumns, ColumnKeys(compiled.FlatteningMap), previousColumns);

        return FormSearchKeys.FromJson(existing?.SearchKeys).Refresh(compile, dateTimeProvider.UtcNow.UtcDateTime);
    }

    /// <inheritdoc cref="SearchCopyRebuilder.RebuildFormAsync"/>
    public Task<int> RebuildAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        rebuilder.RebuildFormAsync(tenantId, formId, cancellationToken);

    private static HashSet<string> ColumnKeys(MergedFormSchema schema) =>
        schema.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
}
