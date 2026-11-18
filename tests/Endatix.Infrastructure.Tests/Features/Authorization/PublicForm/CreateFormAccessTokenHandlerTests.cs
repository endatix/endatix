using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Authorization.PublicForm;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Endatix.Infrastructure.Features.Authorization.PublicForm;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Authorization.PublicForm;

/// <summary>
/// Mint reads the form through <see cref="IFormRuntimeScopeReader"/>, which ignores the request's tenant, so
/// anonymous callers are not blocked by tenant query filters; the token records whether the form was public.
/// </summary>
public sealed class CreateFormAccessTokenHandlerTests
{
    private const long FORM_ID = 42L;
    private const long FORM_TENANT_ID = 99L;

    private readonly IFormRuntimeScopeReader _runtimeScopeReader = Substitute.For<IFormRuntimeScopeReader>();
    private readonly IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> _policy =
        Substitute.For<IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>>();
    private readonly IFormAccessTokenService _tokenService = Substitute.For<IFormAccessTokenService>();
    private readonly ICurrentUserAuthorizationService _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly CreateFormAccessTokenHandler _sut;

    public CreateFormAccessTokenHandlerTests()
    {
        ICachedData<PublicFormAccessData> cached = new Cached<PublicFormAccessData>(
            PublicFormAccessData.CreatePublicForm(FORM_ID),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(10),
            "etag-mint-test");
        _policy.GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ICachedData<PublicFormAccessData>>(cached));
        _sut = new CreateFormAccessTokenHandler(_runtimeScopeReader, _policy, _tokenService, _authorizationService);
    }

    [Fact]
    public async Task Handle_WhenAccessTokenTypeIsFormToken_ReturnsForbiddenWithoutPolicyOrMint()
    {
        var command = new CreateFormAccessTokenCommand(
            FORM_ID,
            accessToken: "existing-form-token",
            accessTokenType: SubmissionTokenType.FormToken);

        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Forbidden);
        await _policy.DidNotReceive().GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>());
        await _runtimeScopeReader.DidNotReceive().GetAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().CreateToken(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(true, false, null, true)]
    [InlineData(true, true, null, true)]
    [InlineData(false, true, null, false)]
    [InlineData(false, false, SubmissionTokenType.AccessToken, false)]
    [InlineData(false, false, null, true)]
    [InlineData(false, false, SubmissionTokenType.SubmissionToken, true)]
    public async Task Handle_MintsForTheFormsTenant_AndGrantsPrivateOnlyOnTheCallersCredentials(
        bool formIsPublic,
        bool callerMayOpenPrivateForms,
        SubmissionTokenType? accessTokenType,
        bool expectedPublicGrant)
    {
        _runtimeScopeReader.GetAsync(FORM_ID, Arg.Any<CancellationToken>())
            .Returns(new FormRuntimeScope(FORM_ID, FORM_TENANT_ID, formIsPublic, IsEnabled: true));
        _authorizationService.HasPermissionAsync(Actions.Access.PrivateForms, Arg.Any<CancellationToken>())
            .Returns(Result.Success(callerMayOpenPrivateForms));
        FormAccessTokenDto dto = new("signed-jwt", DateTime.UtcNow.AddMinutes(60));
        _tokenService.CreateToken(FORM_ID, FORM_TENANT_ID, expectedPublicGrant).Returns(Result.Success(dto));
        var command = new CreateFormAccessTokenCommand(
            FORM_ID,
            accessToken: accessTokenType is null ? null : "caller-token",
            accessTokenType: accessTokenType);

        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(dto);
        _tokenService.Received(1).CreateToken(FORM_ID, FORM_TENANT_ID, expectedPublicGrant);
    }

    [Fact]
    public async Task Handle_WhenTheFormIsDisabled_ReturnsNotFoundWithoutMint()
    {
        _runtimeScopeReader.GetAsync(FORM_ID, Arg.Any<CancellationToken>())
            .Returns(new FormRuntimeScope(FORM_ID, FORM_TENANT_ID, IsPublic: true, IsEnabled: false));

        var result = await _sut.Handle(
            new CreateFormAccessTokenCommand(FORM_ID),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        _tokenService.DidNotReceive().CreateToken(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>());
    }
}
