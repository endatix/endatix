using FastEndpoints;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Security;

namespace Endatix.Api.Endpoints.Auth;

public class Login(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ITokenService tokenService) : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Log in";
            s.Description = "Authenticates a user based on valid credentials and returns JWT token and refresh token";
            s.Responses[200] = "User has been successfully authenticated";
            s.Responses[400] = "The supplied credentials are invalid!";
        });
    }

    public override async Task HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.EmailConfirmed)
        {
            ThrowError("User not found");
        }

        var userVerified = await userManager.CheckPasswordAsync(user, request.Password);
        if (!userVerified)
        {
            ThrowError("Supplied credentials are not valid");
        }

        var userDto = new UserDto(user.Email, [], "SystemInfo");
        var token = tokenService.IssueToken(userDto);
        var refreshToken = GenerateRefreshToken();

        var response = new LoginResponse
        {
            Email = userDto.Email,
            Token = token.Token,
            RefreshToken = refreshToken
        };

        await SendOkAsync(response, cancellationToken);
    }

    private string GenerateRefreshToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}
