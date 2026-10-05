using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Wire model for a person on a form's audience.
/// </summary>
public sealed class AudiencePersonResponse
{
    public long MembershipId { get; init; }

    public long AudienceMemberId { get; init; }

    public string Identifier { get; init; } = string.Empty;

    public IReadOnlyDictionary<long, string> Values { get; init; } =
        new Dictionary<long, string>();

    internal static AudiencePersonResponse FromDto(PersonDto dto) => new()
    {
        MembershipId = dto.MembershipId,
        AudienceMemberId = dto.MemberId,
        Identifier = dto.Identifier,
        Values = dto.Values,
    };
}
