namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// The scope of every kind of <see cref="ITransactionLock"/>. Each kind needs a value of its own, so that its keys
/// never collide with another kind's; keeping them in one place is what keeps them apart.
/// </summary>
public static class TransactionLockScopes
{
    /// <summary>A tenant's audience match key, against the writers that add members under it.</summary>
    public const int AudienceMatchKey = 1116;

    /// <summary>One form's Reporting export schema, so that its rebuilds run one at a time.</summary>
    public const int ReportingFormSchema = 1117;

    /// <summary>The first open of one personalised link, so that parallel opens start one submission.</summary>
    public const int AudienceLinkOpen = 1118;

    /// <summary>One form's submissions: writes that add one share it, the form's deletion takes it exclusively.</summary>
    public const int FormSubmissions = 1119;

    /// <summary>One participant on a form: an edit of their values and their removal, so neither interleaves the other.</summary>
    public const int AudienceMembership = 1120;

    /// <summary>
    /// One form's participants, in stripes keyed <c>{formId}:{stripe}</c>: a removal holds its participant's stripe
    /// exclusively, a CSV import holds every stripe shared, so imports run together and exclude every removal.
    /// </summary>
    public const int AudienceFormPeople = 1121;
}
