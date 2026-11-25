using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;
using Endatix.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Messaging;

public class CatalogHandlerRegistrationTests
{
    [Fact]
    public void Resolve_WithoutTenantContext_DoesNotActivateRepositoryOnlyHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IRepository<DataList>>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IRequestHandler<ListDataListsQuery, Result<Paged<DataListDto>>>>());

        Assert.Contains(nameof(ITenantContext), error.Message);
    }
}
