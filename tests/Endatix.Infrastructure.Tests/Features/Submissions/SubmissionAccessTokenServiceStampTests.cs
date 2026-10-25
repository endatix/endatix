using System.Security.Cryptography;
using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.Submissions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

/// <summary>A stamp binds a token to what issued it: signed with it, and handed back in the claims.</summary>
public class SubmissionAccessTokenServiceStampTests
{
    private const string SigningKey = "validSigningKeyWithAtLeast32CharactersLong";

    private readonly ISubmissionAccessTokenGate _gate = Substitute.For<ISubmissionAccessTokenGate>();

[Fact]
    public void GenerateAndValidate_WithStamp_ReturnsTheStampInTheClaims()
    {
        // Arrange
        var service = CreateService();

        // Act
        var token = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token;
        var result = service.ValidateAccessToken(token);

        // Assert
        token.Split('.').Should().HaveCount(4, "clients read the expiry and permissions from a four-part token");
        token.Split('.')[3].Should().StartWith("0a1b2c3d~");
        result.IsSuccess.Should().BeTrue();
        result.Value.Stamp.Should().Be("0a1b2c3d");
    }

    [Fact]
    public void ValidateAccessToken_WithoutStamp_HasNoStampInTheClaims()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(12345, 60, ["view"]).Value.Token;

        // Act
        var result = service.ValidateAccessToken(token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Stamp.Should().BeNull();
    }

    [Fact]
    public void ValidateAccessToken_TamperedStamp_ReturnsInvalidResult()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token;

        // Act
        var result = service.ValidateAccessToken(token.Replace("0a1b2c3d~", "ffffffff~"));

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_StampRemoved_ReturnsInvalidResult()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token;

        // Act
        var result = service.ValidateAccessToken(token.Replace("0a1b2c3d~", ""));

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("has.dot")]
    [InlineData("NOT-HEX")]
    [InlineData("")]
    public void GenerateAccessToken_StampThatIsNotLowercaseHex_ReturnsInvalidResult(string stamp)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], stamp));

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_TokenSignedBeforeStampsExisted_StillValidates()
    {
        // Arrange: sign "{id}\n{expiry}\n{permissions}" exactly as tokens were signed before stamps.
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes($"12345\n{expiry}\nrw"));
        var signature = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=')[..16];

        // Act
        var result = CreateService().ValidateAccessToken($"12345.{expiry}.rw.{signature}");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionId.Should().Be(12345);
        result.Value.Stamp.Should().BeNull();
    }

    [Fact]
    public void ValidateAccessToken_StampFoldedIntoThePermissions_ReturnsInvalidResult()
    {
        // Arrange: "s\n{stamp}" as the permissions signs the same string as the stamped token.
        var service = CreateService();
        var parts = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token.Split('.');
        var signature = parts[3].Split('~')[1];

        // Act
        var result = service.ValidateAccessToken($"{parts[0]}.{parts[1]}.{parts[2]}\n0a1b2c3d.{signature}");

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_TokenTheGateNoLongerHolds_IsRevoked()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token;
        _gate.IsCurrentAsync(Arg.Is<SubmissionAccessTokenClaims>(claims => claims.Stamp == "0a1b2c3d"), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await service.ValidateAccessTokenAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(error => error.ErrorCode == SubmissionAccessTokenErrors.ErrorCodes.TOKEN_REVOKED);
    }

    private SubmissionAccessTokenService CreateService()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        var options = new SubmissionAccessTokenOptions { AccessTokenSigningKey = SigningKey };
        return new SubmissionAccessTokenService(Options.Create(options), dateTimeProvider, _gate);
    }
}
