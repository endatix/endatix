using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

/// <summary>
/// A signed access token grants its claims whatever the form's privacy, while its form takes submissions, its
/// submission is live and its gate still holds it.
/// </summary>
public partial class PublicFormAccessPolicyTests
{
    private const string RawAccessToken = "100.1893456000.s.0a1b2c3d.DO-NOT-LEAK-sig";

    [Fact]
    public async Task GetAccessDataAsync_AccessToken_PrivateForm_AnonymousUser_GrantsItsClaims()
    {
        // Arrange
        var context = AccessTokenContext(SubmitClaims());
        SetupFormAccessRoutingMetadata(1L, isPublic: false);
        SetupAnonymousUser();

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionId.Should().Be("100");
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.Edit);
    }

    [Fact]
    public async Task GetAccessDataAsync_AccessToken_FormNotTakingSubmissions_ReturnsNotFound()
    {
        // Arrange
        var context = AccessTokenContext(SubmitClaims());
        _submissionRepository
            .AnyAsync(Arg.Any<SubmissionOnOpenFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetAccessDataAsync_AccessToken_TheGateNoLongerHolds_ReturnsNotFound()
    {
        // Arrange
        var claims = SubmitClaims();
        var context = AccessTokenContext(claims);
        _accessTokenService.IsCurrentAsync(claims, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetAccessDataAsync_AccessToken_CachesForAtMostAMinute_WithoutTheRawToken()
    {
        // Arrange
        var context = AccessTokenContext(SubmitClaims());
        (string? Key, HybridCacheEntryOptions? Options) captured = default;
        _cache
            .GetOrCreateAsync<Cached<PublicFormAccessData>>(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = ((string)callInfo[0], (HybridCacheEntryOptions?)callInfo[3]);
                return InvokeHybridCacheGetOrCreateFactory(callInfo);
            });

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        captured.Key.Should().StartWith("ac:form:1:access_token_fp:");
        captured.Key.Should().NotContain(RawAccessToken);
        captured.Key.Should().NotContain("DO-NOT-LEAK");
        captured.Options!.Expiration.Should().Be(TimeSpan.FromMinutes(1));
    }

    /// <summary>Holds every valid token unless a test says otherwise.</summary>
    private static ISubmissionAccessTokenService CurrentAccessTokenService()
    {
        var service = Substitute.For<ISubmissionAccessTokenService>();
        service.IsCurrentAsync(Arg.Any<SubmissionAccessTokenClaims>(), Arg.Any<CancellationToken>()).Returns(true);
        return service;
    }

    private static SubmissionAccessTokenClaims SubmitClaims() =>
        new(100L, [SubmissionAccessTokenPermissions.Submit.Name], DateTime.UtcNow.AddHours(24), "0a1b2c3d");

    private PublicFormAccessContext AccessTokenContext(SubmissionAccessTokenClaims claims)
    {
        SetupAccessTokenValidation(RawAccessToken, true, claims);
        return new PublicFormAccessContext(1L, RawAccessToken, SubmissionTokenType.AccessToken);
    }
}
