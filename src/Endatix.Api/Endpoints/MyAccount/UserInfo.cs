using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.MyAccount;

public class UserInfo : EndpointWithoutRequest<UserInfoResponse>
{
    public override void Configure()
    {
        Get("/my-account/user-info");
        AllowAnonymous();
    }

    public override Task<UserInfoResponse> ExecuteAsync(CancellationToken cancellationToken)
    {
        var claimsDictionary = User?.Claims?
            .DistinctBy(c => c.Type)
            .ToDictionary(c => c.Type, c => c.Value);

        var response = new UserInfoResponse { Claims = claimsDictionary };

        return Task.FromResult(response);
    }
}

public record UserInfoResponse(Dictionary<string, string>? Claims = null);