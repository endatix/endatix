using System.Text.Json;

namespace Endatix.Infrastructure.Data.SeedData;

internal sealed class FormSeedData
{
    public FormInfo Form { get; init; } = null!;
    public DefinitionInfo Definition { get; init; } = null!;
    public List<SubmissionInfo> Submissions { get; init; } = [];
}

internal sealed class FormInfo
{
    public string Name { get; init; } = null!;

    /// <summary>
    /// Optional stable ID. When null, the ID is generated via IIdGenerator.
    /// </summary>
    public long? Id { get; init; }
}

internal sealed class DefinitionInfo
{
    /// <summary>
    /// The SurveyJS form schema as a nested JSON object.
    /// Call <see cref="JsonElement.GetRawText"/> to obtain the serialized string for entity storage.
    /// </summary>
    public JsonElement JsonSchema { get; init; }
}

internal sealed class SubmissionInfo
{
    /// <summary>
    /// The submission data as a nested JSON object.
    /// Call <see cref="JsonElement.GetRawText"/> to obtain the serialized string for entity storage.
    /// </summary>
    public JsonElement JsonData { get; init; }

    /// <summary>
    /// Whether the submission is complete. Defaults to true.
    /// When false, CompletedAt will be null and Status will be "new".
    /// </summary>
    public bool IsComplete { get; init; } = true;

    /// <summary>
    /// The submission status: "new", "read", or "approved". Defaults to "new".
    /// </summary>
    public string Status { get; init; } = "new";
}
