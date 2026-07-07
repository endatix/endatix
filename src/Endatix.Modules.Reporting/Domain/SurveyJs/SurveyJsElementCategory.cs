namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// High-level SurveyJS element classification for export schema compilation.
/// </summary>
/// <remarks>
/// Categories group built-in SurveyJS <c>type</c> values by their role in the form tree and
/// submission shape. They are intentionally separate from <see cref="SurveyJsFlattening"/>:
/// category describes <em>what the element is</em>; flattening describes <em>how many columns
/// it produces</em> (e.g. <c>radiogroup</c> is <see cref="BaseSelect"/> but flattens as
/// <see cref="SurveyJsFlattening.Simple"/>).
/// <para>
/// Aligns with SurveyJS schema inheritance: <c>html</c>, <c>image</c>, and <c>empty</c>
/// extend <c>#nonvalue</c> (no submission payload). <c>expression</c> extends <c>#question</c>
/// and does produce a computed result value.
/// </para>
/// <para>
/// Custom Hub types (e.g. <c>color-picker</c>) are not listed here; they fall through to
/// <see cref="SurveyJsFlattening.Simple"/> unless registered in <see cref="SurveyJsElementType"/>
/// or tenant custom-question metadata (future).
/// </para>
/// </remarks>
internal enum SurveyJsElementCategory
{
    /// <summary>
    /// Display-only elements with no submission value (<c>html</c>, <c>image</c>, <c>empty</c>).
    /// Excluded from the export codebook entirely.
    /// </summary>
    NonData,

    /// <summary>
    /// Structural elements that hold child questions (<c>panel</c>, <c>paneldynamic</c>).
    /// Not emitted as simple columns; children or dedicated flattening paths handle export.
    /// </summary>
    Container,

    /// <summary>
    /// Single-value questions: one schema column per <c>name</c>
    /// (<c>text</c>, <c>boolean</c>, <c>rating</c>, <c>expression</c>, …).
    /// Includes built-in types and the default fallback for unknown custom types.
    /// </summary>
    Scalar,

    /// <summary>
    /// File uploads. Submission value is typically an array of file metadata objects;
    /// flattened to a single export column (joined URLs/names).
    /// </summary>
    File,

    /// <summary>
    /// Choice-based inputs (<c>radiogroup</c>, <c>dropdown</c>, <c>checkbox</c>, <c>tagbox</c>,
    /// <c>imagepicker</c>). Flattening depends on multi-select capabilities.
    /// </summary>
    BaseSelect,

    /// <summary>
    /// Ranking questions: one numeric column per choice reflecting rank position.
    /// </summary>
    Ranking,

    /// <summary>
    /// Multiple text inputs grouped under one question (<c>multipletext</c>).
    /// Submission is a nested object; flattened to one column per item <c>name</c>.
    /// </summary>
    MultipleText,

    /// <summary>
    /// Matrix family (<c>matrix</c>, <c>matrixdropdown</c>, <c>matrixdynamic</c>):
    /// one column per row.
    /// </summary>
    Matrix,
}
