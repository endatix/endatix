using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Email;

public class EmailTemplateServiceTests
{
    private static IOptions<HubSettings> CreateHubOptions(string? hubUrl = "https://app.example.com") =>
        Options.Create(new HubSettings { HubBaseUrl = hubUrl ?? string.Empty });

    [Fact]
    public void Constructor_WithValidSettings_CreatesInstance()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = "https://app.example.com",
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var hubOptions = CreateHubOptions();

        // Act
        var service = new EmailTemplateService(options, hubOptions);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_CreatesInstance()
    {
        // Act
        var service = new EmailTemplateService(null!, CreateHubOptions());

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void CreateVerificationEmail_WithValidInputs_ReturnsCorrectEmailWithTemplate()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = "https://app.example.com",
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var hubOptions = CreateHubOptions("https://app.example.com");
        var service = new EmailTemplateService(options, hubOptions);

        var userEmail = "test@example.com";
        var token = "abc123-token";

        // Act
        var result = service.CreateVerificationEmail(userEmail, token);

        // Assert
        result.Should().NotBeNull();
        result.To.Should().Be(userEmail);
        result.From.Should().Be("noreply@example.com");
        result.Subject.Should().Be(string.Empty); // Subject comes from template
        result.TemplateId.Should().Be("email-verification");
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().HaveCount(6);
        result.Metadata["hubUrl"].Should().Be("https://app.example.com");
        result.Metadata["verificationToken"].Should().Be("abc123-token");
        result.Metadata["verificationUrl"].Should().Be("https://app.example.com/verify-email?token=abc123-token");
        result.Metadata["headline"].Should().Be("Verify your email address");
        result.Metadata["bodyText"].Should().Be("Verify your email address to activate your Endatix account.");
        result.Metadata["actionText"].Should().Be("Verify email address");
    }

    [Fact]
    public void CreateInvitationEmail_WithValidInputs_ReturnsActivationUrl()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            UserInvitation = new EmailTemplateConfig
            {
                TemplateId = "user-invitation",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var hubOptions = CreateHubOptions("https://app.example.com");
        var service = new EmailTemplateService(options, hubOptions);

        var userEmail = "test@example.com";
        var token = "abc123-token";

        // Act
        var result = service.CreateInvitationEmail(userEmail, token);

        // Assert
        result.To.Should().Be(userEmail);
        result.From.Should().Be("noreply@example.com");
        result.TemplateId.Should().Be("user-invitation");
        result.Metadata["activationUrl"].Should().Be("https://app.example.com/activate-invite?token=abc123-token");
        result.Metadata["headline"].Should().Be("Accept your Endatix invitation");
        result.Metadata["bodyText"].Should().Be("Set your password to activate your account and sign in to Endatix Hub.");
        result.Metadata["actionText"].Should().Be("Accept invitation");
    }

    [Fact]
    public void CreateVerificationEmail_WithUrlSensitiveToken_UrlEncodesQueryValue()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var service = new EmailTemplateService(Options.Create(settings), CreateHubOptions("https://app.example.com"));

        // Act
        var result = service.CreateVerificationEmail("test@example.com", "abc+123/==");

        // Assert
        result.Metadata["verificationUrl"].Should().Be("https://app.example.com/verify-email?token=abc%2B123%2F%3D%3D");
    }

    [Fact]
    public void CreateForgotPasswordEmail_WithUrlSensitiveEmail_UrlEncodesQueryValues()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            ForgotPasswordEmail = new EmailTemplateConfig
            {
                TemplateId = "forgot-password",
                FromAddress = "noreply@example.com"
            }
        };
        var service = new EmailTemplateService(Options.Create(settings), CreateHubOptions("https://app.example.com"));

        // Act
        var result = service.CreateForgotPasswordEmail("user+invite@example.com", "reset token");

        // Assert
        result.Metadata["resetCodeQuery"].ToString().Should().StartWith("email=user%2Binvite@example.com&resetCode=");
    }

    [Fact]
    public void CreateVerificationEmail_WithEmptyHubUrl_StillCreatesValidEmail()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = string.Empty,
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var hubOptions = CreateHubOptions(string.Empty);
        var service = new EmailTemplateService(options, hubOptions);

        var userEmail = "test@example.com";
        var token = "abc123-token";

        // Act
        var result = service.CreateVerificationEmail(userEmail, token);

        // Assert
        result.Should().NotBeNull();
        result.Metadata["hubUrl"].Should().Be(string.Empty);
        result.Metadata["verificationToken"].Should().Be("abc123-token");
    }

    [Fact]
    public void CreateVerificationEmail_WithDifferentConfigurations_ReturnsCorrectValues()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = "https://custom-app.com",
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "custom-verification-template",
                FromAddress = "custom@example.com"
            }
        };
        var options = Options.Create(settings);
        var hubOptions = CreateHubOptions(string.Empty); // use fallback from settings
        var service = new EmailTemplateService(options, hubOptions);

        var userEmail = "user@test.com";
        var token = "xyz789-token";

        // Act
        var result = service.CreateVerificationEmail(userEmail, token);

        // Assert
        result.To.Should().Be("user@test.com");
        result.From.Should().Be("custom@example.com");
        result.TemplateId.Should().Be("custom-verification-template");
        result.Metadata["hubUrl"].Should().Be("https://custom-app.com");
        result.Metadata["verificationToken"].Should().Be("xyz789-token");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateVerificationEmail_WithInvalidEmail_StillCreatesEmailWithGivenEmail(string email)
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = "https://app.example.com",
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var service = new EmailTemplateService(options, CreateHubOptions());

        var token = "abc123-token";

        // Act
        var result = service.CreateVerificationEmail(email, token);

        // Assert
        result.Should().NotBeNull();
        result.To.Should().Be(email);
        result.Metadata["verificationToken"].Should().Be("abc123-token");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateVerificationEmail_WithInvalidToken_StillCreatesEmailWithGivenToken(string token)
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            HubUrl = "https://app.example.com",
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "noreply@example.com"
            }
        };
        var options = Options.Create(settings);
        var service = new EmailTemplateService(options, CreateHubOptions());

        var userEmail = "test@example.com";

        // Act
        var result = service.CreateVerificationEmail(userEmail, token);

        // Assert
        result.Should().NotBeNull();
        result.To.Should().Be("test@example.com");
        result.Metadata["verificationToken"].Should().Be(token);
    }
}