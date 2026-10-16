using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// A form's search copy: the keys a compile keeps or a person adds, and the rebuild of the rows when those keys change
/// what the rows should hold.
/// </summary>
internal sealed class FormSearchCopy(IDateTimeProvider dateTimeProvider, SearchCopyRebuilder rebuilder)
{
    internal const string NotFilterableMessage =
        "This column cannot be made filterable: the form's reporting schema has no such column, or no answer filter supports it.";

    /// <summary>The search keys of <paramref name="existing"/> after <paramref name="compiled"/> is saved over it.</summary>
    public SearchKeysRefresh KeysAfter(FormSchemaEntity? existing, FormSchemaCompileResult compiled)
    {
        IReadOnlySet<string> previousColumns = existing is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : ColumnKeys(FormSchemaFlatteningMap.FromJson(existing.FlatteningMap));
        SearchKeysCompile compile = new(compiled.SearchableColumns, ColumnKeys(compiled.FlatteningMap), previousColumns);

        var searchKeys = existing is null ? FormSearchKeys.Empty : FormSearchKeys.Of(existing);
        return searchKeys.Refresh(compile, dateTimeProvider.UtcNow.UtcDateTime);
    }

    /// <summary>
    /// The schema's keys with <paramref name="key"/> added by a person (<see cref="FormSearchKeys.AddUser"/>); invalid
    /// when the schema has no such column or no answer filter supports it. Read-only questions may be added.
    /// </summary>
    public Result<FormSearchKeys> AddUserKey(FormSchemaEntity schema, string key) =>
        SearchableColumns.ChosenColumn(schema, key) is { } column
            ? FormSearchKeys.Of(schema).AddUser(column, dateTimeProvider.UtcNow.UtcDateTime)
            : Result<FormSearchKeys>.Invalid(new ValidationError("key", NotFilterableMessage, "NotFilterable", ValidationSeverity.Error));

    /// <inheritdoc cref="SearchCopyRebuilder.RebuildFormAsync"/>
    public Task<int> RebuildAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        rebuilder.RebuildFormAsync(tenantId, formId, cancellationToken);

    private static HashSet<string> ColumnKeys(MergedFormSchema schema) =>
        schema.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
}
