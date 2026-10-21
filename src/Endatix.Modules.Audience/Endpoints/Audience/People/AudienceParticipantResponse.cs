using System.Text.Json.Serialization;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Wire model for one participant: the list item's fields, plus the newest 50 of their submissions on the form and
/// how many they have in all.
/// </summary>
public sealed class AudienceParticipantResponse : AudiencePersonResponse
{
    private AudienceParticipantResponse(ParticipantDto dto)
        : base(dto.Participant)
    {
        Submissions = dto.Submissions.Select(AudienceParticipantSubmissionResponse.From).ToList();
        SubmissionTotal = dto.SubmissionTotal;
    }

    /// <summary>Newest first: the submission the link opened, the Submitter's, and Snapshots that name them (test or on-behalf).</summary>
    public IReadOnlyList<AudienceParticipantSubmissionResponse> Submissions { get; init; }

    /// <summary>How many submissions the participant has on the form; <see cref="Submissions"/> holds at most 50.</summary>
    public int SubmissionTotal { get; init; }

    internal static AudienceParticipantResponse FromDto(ParticipantDto dto) => new(dto);
}

/// <summary>Wire model for one of a participant's submissions.</summary>
public sealed class AudienceParticipantSubmissionResponse
{
    /// <summary>The submission id.</summary>
    public long Id { get; init; }

    /// <summary>When the submission was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When it was completed; <see langword="null"/> while in progress.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Whether the submission is complete.</summary>
    public bool IsComplete { get; init; }

    /// <summary>The submission status code, such as <c>new</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Whether it is a test submission.</summary>
    public bool IsTest { get; init; }

    /// <summary><c>link</c>, <c>on_behalf</c> or <c>test</c>; left out when the submission has no Snapshot.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SnapshotSource { get; init; }

    internal static AudienceParticipantSubmissionResponse From(ParticipantSubmissionDto dto) => new()
    {
        Id = dto.Id,
        CreatedAt = dto.CreatedAt,
        CompletedAt = dto.CompletedAt,
        IsComplete = dto.IsComplete,
        Status = dto.Status,
        IsTest = dto.IsTest,
        SnapshotSource = dto.SnapshotSource,
    };
}
