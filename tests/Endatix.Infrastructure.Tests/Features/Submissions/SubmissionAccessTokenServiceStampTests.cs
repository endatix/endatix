using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.Submissions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

/// <summary>A stamp binds a token to what issued it: signed with it, and handed back in the claims.</summary>
public class SubmissionAccessTokenServiceStampTests
{
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
        token.Split('.').Should().HaveCount(5);
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
        var parts = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token.Split('.');
        parts[3] = "ffffffff";

        // Act
        var result = service.ValidateAccessToken(string.Join('.', parts));

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void ValidateAccessToken_StampRemoved_ReturnsInvalidResult()
    {
        // Arrange
        var service = CreateService();
        var parts = service.GenerateAccessToken(new SubmissionAccessTokenRequest(12345, 60, ["submit"], "0a1b2c3d")).Value.Token.Split('.');

        // Act
        var result = service.ValidateAccessToken(string.Join('.', parts[0], parts[1], parts[2], parts[4]));

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
    public async Task IsCurrentAsync_AsksTheGate()
    {
        // Arrange
        var claims = new SubmissionAccessTokenClaims(12345, ["submit"], DateTime.UtcNow.AddHours(1), "0a1b2c3d");
        _gate.IsCurrentAsync(claims, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var isCurrent = await CreateService().IsCurrentAsync(claims, TestContext.Current.CancellationToken);

        // Assert
        isCurrent.Should().BeFalse();
    }

    private SubmissionAccessTokenService CreateService()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        var options = new SubmissionAccessTokenOptions { AccessTokenSigningKey = "validSigningKeyWithAtLeast32CharactersLong" };
        return new SubmissionAccessTokenService(Options.Create(options), dateTimeProvider, _gate);
    }
}
