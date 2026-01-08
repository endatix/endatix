using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionAccessTokenServiceTests
{
    private readonly SubmissionAccessTokenOptions _validOptions = new()
    {
        AccessTokenSigningKey = "validSigningKeyWithAtLeast32CharactersLong"
    };

    private IDateTimeProvider CreateDateTimeProvider(DateTimeOffset? now = null)
    {
        var provider = Substitute.For<IDateTimeProvider>();
        provider.Now.Returns(now ?? DateTimeOffset.UtcNow);
        return provider;
    }

    [Fact]
    public void Constructor_ValidSigningKey_CreatesInstance()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();

        // Act
        var act = () => new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsException()
    {
        // Arrange
        var dateTimeProvider = CreateDateTimeProvider();

        // Act
        var act = () => new SubmissionAccessTokenService(null!, dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullDateTimeProvider_ThrowsException()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);

        // Act
        var act = () => new SubmissionAccessTokenService(optionsWrapper, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullSigningKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = new SubmissionAccessTokenOptions
        {
            AccessTokenSigningKey = null!
        };
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var dateTimeProvider = CreateDateTimeProvider();
        var act = () => new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptySigningKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = new SubmissionAccessTokenOptions
        {
            AccessTokenSigningKey = ""
        };
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var dateTimeProvider = CreateDateTimeProvider();
        var act = () => new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_SigningKeyTooShort_ThrowsException()
    {
        // Arrange
        var invalidOptions = new SubmissionAccessTokenOptions
        {
            AccessTokenSigningKey = "short"
        };
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var dateTimeProvider = CreateDateTimeProvider();
        var act = () => new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Signing key must be at least 32 characters.");
    }

    [Fact]
    public void GenerateAccessToken_ValidParameters_ReturnsSuccessWithToken()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var submissionId = 12345L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };

        // Act
        var result = service.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(expiryMinutes), TimeSpan.FromSeconds(5));
        result.Value.Permissions.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void GenerateAccessToken_TokenFormat_ShouldHaveFourDotSeparatedFields()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var submissionId = 12345L;
        var expiryMinutes = 60;
        var permissions = new[] { "view" };

        // Act
        var result = service.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tokenParts = result.Value.Token.Split('.');
        tokenParts.Should().HaveCount(4);
        tokenParts[0].Should().Be(submissionId.ToString());
        tokenParts[2].Should().Be("r"); // view permission code
    }

    [Fact]
    public void GenerateAccessToken_MultiplePermissions_EncodesCorrectly()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var submissionId = 12345L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit", "export" };

        // Act
        var result = service.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tokenParts = result.Value.Token.Split('.');
        tokenParts[2].Should().Be("rwx"); // All permission codes
    }

    [Fact]
    public void GenerateAccessToken_ZeroSubmissionId_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.GenerateAccessToken(0, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateAccessToken_NegativeSubmissionId_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.GenerateAccessToken(-1, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateAccessToken_ZeroExpiryMinutes_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.GenerateAccessToken(12345, 0, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateAccessToken_NegativeExpiryMinutes_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.GenerateAccessToken(12345, -1, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateAccessToken_EmptyPermissions_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.GenerateAccessToken(12345, 60, Array.Empty<string>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateAccessToken_InvalidPermission_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.GenerateAccessToken(12345, 60, new[] { "invalid" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateAccessToken_MixedValidAndInvalidPermissions_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.GenerateAccessToken(12345, 60, new[] { "view", "invalid" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_ValidToken_ReturnsSuccessWithClaims()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var submissionId = 12345L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };
        var tokenResult = service.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        // Act
        var result = service.ValidateAccessToken(tokenResult.Value.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SubmissionId.Should().Be(submissionId);
        result.Value.Permissions.Should().BeEquivalentTo(permissions);
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(expiryMinutes), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ValidateAccessToken_NullToken_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.ValidateAccessToken(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateAccessToken_EmptyToken_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var act = () => service.ValidateAccessToken("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateAccessToken_InvalidFormat_TooFewParts_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.ValidateAccessToken("12345.123456.rw");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_InvalidFormat_TooManyParts_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.ValidateAccessToken("12345.123456.rw.sig.extra");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_InvalidSubmissionId_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.ValidateAccessToken("notanumber.123456.rw.signature");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_InvalidExpiry_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.ValidateAccessToken("12345.notanumber.rw.signature");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_InvalidSignature_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var tokenResult = service.GenerateAccessToken(12345, 60, new[] { "view" });
        var tokenParts = tokenResult.Value.Token.Split('.');

        // Tamper with the signature
        var tamperedToken = $"{tokenParts[0]}.{tokenParts[1]}.{tokenParts[2]}.invalidsignature";

        // Act
        var result = service.ValidateAccessToken(tamperedToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_TamperedSubmissionId_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var tokenResult = service.GenerateAccessToken(12345, 60, new[] { "view" });
        var tokenParts = tokenResult.Value.Token.Split('.');

        // Tamper with the submission ID
        var tamperedToken = $"99999.{tokenParts[1]}.{tokenParts[2]}.{tokenParts[3]}";

        // Act
        var result = service.ValidateAccessToken(tamperedToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_TamperedPermissions_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var tokenResult = service.GenerateAccessToken(12345, 60, new[] { "view" });
        var tokenParts = tokenResult.Value.Token.Split('.');

        // Tamper with the permissions
        var tamperedToken = $"{tokenParts[0]}.{tokenParts[1]}.rwx.{tokenParts[3]}";

        // Act
        var result = service.ValidateAccessToken(tamperedToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_EmptyPermissions_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var tokenResult = service.GenerateAccessToken(12345, 60, new[] { "view" });
        var tokenParts = tokenResult.Value.Token.Split('.');

        // Create token with empty permissions
        var emptyPermToken = $"{tokenParts[0]}.{tokenParts[1]}..{tokenParts[3]}";

        // Act
        var result = service.ValidateAccessToken(emptyPermToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void GenerateAndValidate_RoundTrip_PreservesAllData()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);
        var submissionId = 12345L;
        var expiryMinutes = 120;
        var permissions = new[] { "view", "edit", "export" };

        // Act
        var generateResult = service.GenerateAccessToken(submissionId, expiryMinutes, permissions);
        var validateResult = service.ValidateAccessToken(generateResult.Value.Token);

        // Assert
        generateResult.IsSuccess.Should().BeTrue();
        validateResult.IsSuccess.Should().BeTrue();
        validateResult.Value.SubmissionId.Should().Be(submissionId);
        validateResult.Value.Permissions.Should().BeEquivalentTo(permissions);
        validateResult.Value.ExpiresAt.Should().BeCloseTo(generateResult.Value.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GenerateAccessToken_DifferentSigningKeys_ProduceDifferentSignatures()
    {
        // Arrange
        var options1 = new SubmissionAccessTokenOptions
        {
            AccessTokenSigningKey = "firstSigningKeyWithAtLeast32Characters"
        };
        var options2 = new SubmissionAccessTokenOptions
        {
            AccessTokenSigningKey = "secondSigningKeyWithAtLeast32CharactersLong"
        };
        var optionsWrapper1 = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper1.Value.Returns(options1);
        var optionsWrapper2 = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper2.Value.Returns(options2);

        var dateTimeProvider = CreateDateTimeProvider();
        var service1 = new SubmissionAccessTokenService(optionsWrapper1, dateTimeProvider);
        var service2 = new SubmissionAccessTokenService(optionsWrapper2, dateTimeProvider);

        var submissionId = 12345L;
        var expiryMinutes = 60;
        var permissions = new[] { "view" };

        // Act
        var token1 = service1.GenerateAccessToken(submissionId, expiryMinutes, permissions);
        var token2 = service2.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        // Assert
        token1.Value.Token.Should().NotBe(token2.Value.Token);

        // Token generated by service1 should not validate with service2
        var crossValidation = service2.ValidateAccessToken(token1.Value.Token);
        crossValidation.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void GenerateAccessToken_URLSafeSignature_ContainsNoSpecialCharacters()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var dateTimeProvider = CreateDateTimeProvider();
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        // Act
        var result = service.GenerateAccessToken(12345, 60, new[] { "view" });

        // Assert
        result.Value.Token.Should().NotContain("+");
        result.Value.Token.Should().NotContain("/");
        result.Value.Token.Should().NotContain("=");
    }

    [Fact]
    public void ValidateAccessToken_ExpiredToken_ReturnsInvalidResult()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dateTimeProvider = CreateDateTimeProvider(now);

        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        var expiryMinutes = 60;
        var tokenResult = service.GenerateAccessToken(12345, expiryMinutes, new[] { "view" });

        // Advance time past expiry
        var futureTime = now.AddMinutes(expiryMinutes + 1);
        dateTimeProvider.Now.Returns(futureTime);

        // Act
        var result = service.ValidateAccessToken(tokenResult.Value.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("expired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAccessToken_TokenJustBeforeExpiry_ReturnsSuccess()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dateTimeProvider = CreateDateTimeProvider(now);

        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        var expiryMinutes = 60;
        var tokenResult = service.GenerateAccessToken(12345, expiryMinutes, new[] { "view" });

        // Advance time to just before expiry (59 minutes)
        var almostExpired = now.AddMinutes(expiryMinutes - 1);
        dateTimeProvider.Now.Returns(almostExpired);

        // Act
        var result = service.ValidateAccessToken(tokenResult.Value.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionId.Should().Be(12345);
    }

    [Fact]
    public void ValidateAccessToken_TokenAtExactExpiry_ReturnsInvalidResult()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dateTimeProvider = CreateDateTimeProvider(now);

        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        var expiryMinutes = 60;
        var tokenResult = service.GenerateAccessToken(12345, expiryMinutes, new[] { "view" });

        // Advance time to exact expiry
        var exactExpiry = now.AddMinutes(expiryMinutes);
        dateTimeProvider.Now.Returns(exactExpiry);

        // Act
        var result = service.ValidateAccessToken(tokenResult.Value.Token);

        // Assert - at exact expiry, token should be expired (> not >=)
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_VeryShortLivedToken_ExpiresCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dateTimeProvider = CreateDateTimeProvider(now);

        var optionsWrapper = Substitute.For<IOptions<SubmissionAccessTokenOptions>>();
        optionsWrapper.Value.Returns(_validOptions);
        var service = new SubmissionAccessTokenService(optionsWrapper, dateTimeProvider);

        var expiryMinutes = 1; // Very short-lived token
        var tokenResult = service.GenerateAccessToken(12345, expiryMinutes, new[] { "view" });

        // Validate immediately - should succeed
        var immediateResult = service.ValidateAccessToken(tokenResult.Value.Token);
        immediateResult.IsSuccess.Should().BeTrue();

        // Advance time past expiry
        dateTimeProvider.Now.Returns(now.AddMinutes(2));

        // Act
        var expiredResult = service.ValidateAccessToken(tokenResult.Value.Token);

        // Assert
        expiredResult.IsSuccess.Should().BeFalse();
        expiredResult.Status.Should().Be(ResultStatus.Invalid);
    }
}
