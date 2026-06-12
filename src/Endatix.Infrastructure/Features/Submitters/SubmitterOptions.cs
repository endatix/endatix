using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Options for the submitter feature.
/// </summary>
public sealed class SubmitterOptions : EndatixOptionsBase
{
    /// <inheritdoc />
    public override string SectionPath => "Submitter";

    /// <summary>
    /// The claim types to use for the display ID.
    /// </summary>
    public List<string> DisplayIdClaimTypes { get; set; } =
    [
        "panelistId",
        "preferred_username"
    ];

    public List<string> ProfileSnapshotFields { get; set; } = [];
}
