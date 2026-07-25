namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Controls whether FormSchema compile merges historical columns/questions or rebuilds from the current definition only.
/// </summary>
public enum FormSchemaCompileMode
{
    /// <summary>
    /// Append-only merge: retain historical FlatteningMap keys and codebook entries.
    /// </summary>
    Merge = 0,

    /// <summary>
    /// Replace: rebuild FlatteningMap and codebook from the current definition only.
    /// </summary>
    Replace = 1,
}
