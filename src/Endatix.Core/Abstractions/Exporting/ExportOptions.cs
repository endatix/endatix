using System.Collections.Generic;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Options for customizing export operations.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Optional columns to include in the export. If null, all columns are included.
    /// </summary>
    public IEnumerable<string>? Columns { get; set; }

    /// <summary>
    /// Optional dictionary of column transformers to customize how values are formatted.
    /// Keys are column names, values are functions that transform the original value to a string.
    /// </summary>
    public IDictionary<string, Func<object?, string>>? Transformers { get; set; }

    /// <summary>
    /// Optional metadata for export operations. Can be used to pass additional context.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
}