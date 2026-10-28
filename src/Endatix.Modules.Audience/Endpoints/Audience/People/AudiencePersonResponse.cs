using System.Text.Json.Serialization;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Wire model for a person on a form's audience.
/// </summary>
public class AudiencePersonResponse
{
    /// <summary>An empty response, filled by its initializers.</summary>
    public AudiencePersonResponse()
    {
    }

    /// <summary>The participant's fields, for this response and the ones that extend it.</summary>
    protected AudiencePersonResponse(PersonDto dto)
    {
        MembershipId = dto.MembershipId;
        AudienceMemberId = dto.MemberId;
        Identifier = dto.Identifier;
        Values = dto.Values;
        IsTest = dto.IsTest;
        Link = AudienceLinkStatusResponse.From(dto.Link);
        NotFilledIn = dto.NotFilledIn;
    }

    public long MembershipId { get; init; }

    public long AudienceMemberId { get; init; }

    public string Identifier { get; init; } = string.Empty;

    public IReadOnlyDictionary<long, string> Values { get; init; } =
        new Dictionary<long, string>();

    /// <summary>A test person: never issued a personalised link. Set when added and never changed.</summary>
    public bool IsTest { get; init; }

    /// <summary>Whether the person's personalised link was issued and opened.</summary>
    public AudienceLinkStatusResponse Link { get; init; } = AudienceLinkStatusResponse.From(LinkStatusDto.None);

    /// <summary>
    /// How many of the form's Properties the person has no value for. On the people list only; left out elsewhere.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NotFilledIn { get; init; }

    internal static AudiencePersonResponse FromDto(PersonDto dto) => new(dto);
}

/// <summary>
/// Wire model for a Link status: <c>none</c>, <c>issued</c>, or <c>opened</c> with when and, while it is live, the
/// submission the link opened.
/// </summary>
public sealed class AudienceLinkStatusResponse
{
    /// <summary><c>none</c>, <c>issued</c> or <c>opened</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>When the link opened its submission; left out unless <see cref="Status"/> is <c>opened</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? OpenedAt { get; init; }

    /// <summary>The submission the link opened, while it is live; left out once it was deleted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SubmissionId { get; init; }

    internal static AudienceLinkStatusResponse From(LinkStatusDto dto) => new()
    {
        Status = dto.Status switch
        {
            LinkStatusKind.Issued => "issued",
            LinkStatusKind.Opened => "opened",
            _ => "none",
        },
        OpenedAt = dto.OpenedAt,
        SubmissionId = dto.SubmissionId,
    };
}
