using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantSettings;
using Settings = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.TenantSettings;

public sealed class TenantSettingsAccessTests
{
    [Fact]
    public async Task Handle_MissingTenant_PreservesEachOperationRefusalBeforeAnyLookup()
    {
        // Arrange
        var settings = Substitute.For<IRepository<Settings>>();
        var snapshot = Substitute.For<ICurrentUserAuthorizationService>();
        var access = new TenantSettingsAccessHandler(settings, Substitute.For<ITenantContext>(), new AccountStanding(snapshot));
        var ct = TestContext.Current.CancellationToken;

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => access.Handle(new TenantSettingsAccess.Read(), ct));
        var update = await access.Handle(new TenantSettingsAccess.Update(), ct);
        Assert.Equal(ResultStatus.NotFound, update.Status);
        Assert.Empty(snapshot.ReceivedCalls());
        Assert.Empty(settings.ReceivedCalls());
    }
}
