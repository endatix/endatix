using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantSettings.Get;
using Endatix.Core.UseCases.TenantSettings.PartialUpdate;
using MediatR;
using Settings = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.TenantSettings;

public sealed class TenantSettingsAccessTests
{
    [Fact]
    public async Task Handle_MissingTenant_PreservesEachOperationRefusalBeforeAnyLookup()
    {
        // Arrange
        var settings = Substitute.For<IRepository<Settings>>();
        var tenant = Substitute.For<ITenantContext>();
        var read = new GetTenantSettingsHandler(settings, tenant);
        var patch = new PartialUpdateTenantSettingsHandler(settings, tenant, Substitute.For<IMediator>());
        var ct = TestContext.Current.CancellationToken;

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => read.Handle(new GetTenantSettingsQuery(), ct));
        var update = await patch.Handle(new PartialUpdateTenantSettingsCommand(), ct);
        Assert.Equal(ResultStatus.NotFound, update.Status);
        Assert.Empty(settings.ReceivedCalls());
    }
}
