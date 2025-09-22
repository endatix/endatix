using FastEndpoints;

namespace Endatix.Api.Endpoints.MyAccount;

public class UserInfo : EndpointWithoutRequest<UserInfoResponse>
{
    public override void Configure()
    {
        Get("/my-account/user-info");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get current user information";
            s.Description = "Returns the current authenticated user's claims and information";
            s.Responses[200] = "User information retrieved successfully";
            s.Responses[401] = "User is not authenticated";
        });
    }

    public override Task<UserInfoResponse> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Group claims by type to handle multiple values (like permissions)
        var claimsGrouped = User?.Claims?
            .GroupBy(c => c.Type)
            .ToDictionary(
                g => g.Key, 
                g => g.Count() == 1 ? g.First().Value : string.Join(", ", g.Select(c => c.Value))
            );

        var response = new UserInfoResponse { Claims = claimsGrouped };

        return Task.FromResult(response);
    }
}

public record UserInfoResponse(Dictionary<string, string>? Claims = null);