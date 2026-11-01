namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// One key of a form's search copy: the flattened column it copies, who chose it, the answer filter it serves, and
/// when it was added.
/// </summary>
/// <param name="Key">The flattened column key.</param>
/// <param name="Source"><see cref="SearchKeySources.Auto"/> or <see cref="SearchKeySources.User"/>.</param>
/// <param name="FilterType">A <c>SubmissionGridFilterTypes</c> value; decides how the value is normalised.</param>
/// <param name="AddedAt">When the key was added, in UTC.</param>
/// <param name="Revision">
/// The form's search keys revision (<see cref="FormSearchKeys.Revision"/>) at which rows began to need this key's
/// values: a row stamped with an older revision holds no value for it yet, so it cannot be checked on this key.
/// </param>
internal sealed record SearchKey(string Key, string Source, string FilterType, DateTime AddedAt, int Revision = 0);

internal static class SearchKeySources
{
    /// <summary>Chosen by the schema compile: an eligible question, in schema order.</summary>
    public const string Auto = "auto";

    /// <summary>Added by a person, in the slots the automatic keys leave free.</summary>
    public const string User = "user";
}
