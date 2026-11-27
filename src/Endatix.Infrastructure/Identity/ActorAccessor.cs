using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity;

internal sealed class ActorAccessor(IHttpContextAccessor http) : IActorAccessor
{
    public Actor Current
    {
        get
        {
            if (http.HttpContext is not { } context)
            {
                return Actor.System;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("An author must be authenticated to write a draft.");
            }

            var identity = context.User.Identities.OfType<AuthorizedIdentity>().FirstOrDefault();
            return new Actor("user", identity?.LocalUserId);
        }
    }
}
