using Endatix.Api.Common;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Validator for the ListUsersRequest.
/// </summary>
public class ListUsersValidator : Validator<ListUsersRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListUsersValidator"/> class.
    /// </summary>
    public ListUsersValidator()
    {
        Include(new PagedRequestValidator());
    }
}
