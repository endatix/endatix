namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// The folders a Shoji codebook's <c>order</c> groups variables into; Crunch shows each as a folder. Panels and loops
/// are sub-folders of <see cref="Questions"/>, named by their title.
/// </summary>
internal static class ShojiFolderNames
{
    /// <summary>System columns. A folder label only: in code the group is the system columns.</summary>
    public const string MetaData = "Meta-data";

    /// <summary>The audience identifier and the Snapshot's values.</summary>
    public const string ParticipantVariables = "Participant variables";

    /// <summary>Submitter fields captured with the response.</summary>
    public const string Submitter = "Submitter";

    public const string CalculatedValues = "Calculated values";

    public const string Questions = "Questions";
}
