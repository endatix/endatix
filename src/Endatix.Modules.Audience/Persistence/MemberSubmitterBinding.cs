using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>A person and the Submitter their responses are created under.</summary>
internal readonly record struct SubmitterBinding(long MemberId, long SubmitterId);

internal static class MemberSubmitterBinding
{
    /// <summary>
    /// Sets <see cref="Member.SubmitterId"/> once. The update matches only a member with no Submitter yet, so
    /// parallel writers (an on-behalf create, a link open) cannot re-point it.
    /// </summary>
    public static Task<int> BindSubmitterOnceAsync(
        this IAudienceDbContext db,
        SubmitterBinding binding,
        CancellationToken cancellationToken) =>
        db.Members
            .Where(person => person.Id == binding.MemberId && person.SubmitterId == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(person => person.SubmitterId, binding.SubmitterId),
                cancellationToken);
}
