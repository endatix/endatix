
using System.ComponentModel.DataAnnotations;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Api.Endpoints.Auth;

public class Register : Endpoint<RegisterRequest, RegisterResponse>
{

    // Validate the email address using DataAnnotations like the UserValidator does when RequireUniqueEmail = true.
    private static readonly EmailAddressAttribute _emailAddressAttribute = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly IUserStore<AppUser> _userStore;

    public Register(UserManager<AppUser> userManager, IUserStore<AppUser> userStore)
    {
        _userManager = userManager;
        _userStore = userStore;
    }

    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Register new user";
            s.Description = "Registers new user for the Endatix application";
            s.Responses[200] = "Use has been successfully registered";
            s.Responses[400] = "The supplied user information is invalid.";
        });
    }

    public override async Task HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException($"{nameof(Register)} endpoint requires a user store with email support.");
        }

        var email = request.Email;

        if (string.IsNullOrEmpty(email) || !_emailAddressAttribute.IsValid(email))
        {
            ThrowError("The email is invalid");
        }

        var user = new AppUser
        {
            EmailConfirmed = true
        };

        var emailStore = (IUserEmailStore<AppUser>)_userStore;
        await _userStore.SetUserNameAsync(user, email, cancellationToken);
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errorMessage = string.Empty;
            result.Errors.ToList().ForEach(error => errorMessage += error.Description + "\n");
            ThrowError($"Cannot register user: {errorMessage}");
        }

        RegisterResponse successfulResponse = new()
        {
            Success = true,
            Message = "User has been successfully registered"
        };

        await SendOkAsync(successfulResponse, cancellationToken);
    }
}

/// <summary>
/// The request type for the "/register" endpoint added by <see cref="Register.HandleAsync"/> method.
/// </summary>
public record RegisterRequest(string Email, string Password, string ConfirmPassword);

public record RegisterResponse(bool Success = false, string Message = "");

public class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}