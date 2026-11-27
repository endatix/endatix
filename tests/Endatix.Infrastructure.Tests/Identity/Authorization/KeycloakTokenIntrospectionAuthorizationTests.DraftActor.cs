using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class KeycloakTokenIntrospectionAuthorizationTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, 123L)]
    public async Task DraftActor_NumericExternalSubject_UsesOnlyTheProvisionedAccount(bool provisioned, long? expectedId)
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TEST_ISSUER, activate: true);
        context.SetSuccessfulIntrospectionResponse(["writer"]);
        var permissions = provisioned ? new[] { Actions.Forms.Edit, Actions.Access.Hub } : [Actions.Forms.Edit];
        context.Mapper.Result = IExternalAuthorizationMapper.MappingResult.Success([], permissions);
        var principal = CreatePrincipal("456", TEST_ISSUER);
        var authorization = await context.Strategy.GetAuthorizationDataAsync(principal, Xunit.TestContext.Current.CancellationToken);
        principal.AddIdentity(new AuthorizedIdentity(authorization.Value));
        context.HttpContextAccessor.HttpContext!.User = principal;

        // Act
        var actor = new ActorAccessor(context.HttpContextAccessor).Current;

        // Assert
        actor.Kind.Should().Be("user");
        actor.UserId.Should().Be(expectedId);
    }
}
