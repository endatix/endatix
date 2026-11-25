using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>An invite-specific conflict, distinct from conflicts returned by registration.</summary>
[Experimental("ENDATIX0007")]
public interface IInviteDeclined;

internal sealed class InviteDeclined<T> : Result<T>, IInviteDeclined, IMappableError
{
    internal InviteDeclined() : base(ResultStatus.Conflict)
    {
        Errors = ["This workspace is not eligible to invite teammates yet."];
    }

    public Result<TDestination> MapError<TDestination>() => new InviteDeclined<TDestination>();
}
