using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class implements ASP.NET IdentityRole used for idenity Role persistence.
/// using <see cref="long" /> type to match the <see cref="Endatix.Core.Abstractions.IIdGenerator" />
/// </summary>
public class AppRole : IdentityRole<long>
{

    /// <summary>
    /// Description for the role. 
    /// </summary>
    public string? Description { get; private set; }


    internal Role ToRoleEntity()
    {
        Guard.Against.NullOrEmpty(Name);

        var role = new Role(
            id: Id,
            name: Name,
            description: Description
        );

        return role;
    }
}