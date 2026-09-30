namespace Endatix.Modules.Personalization.Contracts;

/// <summary>
/// Wire codes for <c>AudienceProperty.DataType</c>.
/// </summary>
public static class AudienceDataTypeCodes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string Date = "date";
    public const string DateTime = "date_time";
    public const string SingleChoice = "single_choice";
    public const string MultipleChoice = "multiple_choice";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Text,
        Number,
        Boolean,
        Date,
        DateTime,
        SingleChoice,
        MultipleChoice,
    };

    public static bool IsKnown(string? dataType) =>
        dataType is not null && All.Contains(dataType);
}
