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
    public async Task Handle_MissingTenant_ReadRejectsAnInvalidContextBeforeLookup()
    {
        // Arrange
        var settings = Substitute.For<IRepository<Settings>>();
        var tenant = Substitute.For<ITenantContext>();
        var read = new GetTenantSettingsHandler(settings, tenant);
        var ct = TestContext.Current.CancellationToken;

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => read.Handle(new GetTenantSettingsQuery(), ct));
        Assert.Empty(settings.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_MissingTenant_RegisteredReadReportsInvalidContextBeforeLookup()
    {
        var settings = Substitute.For<IRepository<Settings>>();
        var read = new GetTenantSettingsHandler.Registered(settings, Substitute.For<ITenantContext>());
        var result = await read.Handle(new GetTenantSettingsQuery(), TestContext.Current.CancellationToken);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(settings.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_MissingTenant_UpdateReturnsNotFoundWithoutLookup()
    {
        // Arrange
        var settings = Substitute.For<IRepository<Settings>>();
        var patch = new PartialUpdateTenantSettingsHandler(settings, Substitute.For<ITenantContext>(), Substitute.For<IMediator>());
        var ct = TestContext.Current.CancellationToken;

        // Act
        var update = await patch.Handle(new PartialUpdateTenantSettingsCommand(), ct);

        // Assert
        Assert.Equal(ResultStatus.NotFound, update.Status);
        Assert.Empty(settings.ReceivedCalls());
    }
}
