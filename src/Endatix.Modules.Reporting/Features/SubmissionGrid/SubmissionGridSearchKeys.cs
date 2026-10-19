namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// A form's search keys: the columns answer filters can read, and how many slots are left.
/// </summary>
/// <param name="FormId">The form.</param>
/// <param name="Keys">The search keys, automatic ones first in schema order, then the ones people added.</param>
/// <param name="Slots">How many keys the form has and may have.</param>
public sealed record SubmissionGridSearchKeys(
    long FormId,
    IReadOnlyList<SubmissionGridSearchKey> Keys,
    SubmissionGridSearchSlots Slots);

/// <summary>
/// One column answer filters can read.
/// </summary>
/// <param name="Key">The column key.</param>
/// <param name="Source"><c>auto</c> when the schema chose it, <c>user</c> when a person added it.</param>
/// <param name="FilterType">One of <see cref="SubmissionGridFilterTypes"/>.</param>
/// <param name="Pending">
/// Whether some prepared rows have not been rebuilt with this key yet. Filters on it count those rows as not checked.
/// </param>
public sealed record SubmissionGridSearchKey(string Key, string Source, string FilterType, bool Pending);

/// <summary>
/// How many search keys a form has, and its caps: <paramref name="Max"/> in all, of which people may add
/// <paramref name="UserMax"/>.
/// </summary>
/// <param name="Used">Search keys of the form.</param>
/// <param name="Max">Search keys a form may have.</param>
/// <param name="UserUsed">Search keys people added.</param>
/// <param name="UserMax">Search keys people may add.</param>
public sealed record SubmissionGridSearchSlots(int Used, int Max, int UserUsed, int UserMax);
