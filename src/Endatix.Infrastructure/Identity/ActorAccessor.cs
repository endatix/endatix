using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Identity;

internal sealed class ActorAccessor(IUserContext users) : IActorAccessor
{
    public Actor Current
    {
        get
        {
            if (!users.IsAuthenticated)
            {
                return Actor.System;
            }

            var userId = users.GetActorUserId() ??
                (long.TryParse(users.GetCurrentUserId(), out var id) ? id : (long?)null);
            return new Actor("user", userId);
        }
    }
}
