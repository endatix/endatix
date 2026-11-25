using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Messaging;

public class CatalogHandlerRegistrationTests
{
    [Fact]
    public void Resolve_locales_without_tenant_does_not_use_the_direct_repository()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDataListRepository>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<IRequestHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>>());
        Assert.Contains(nameof(ITenantContext), error.Message);
    }

    [Fact]
    public void Resolve_import_with_a_typed_logger_does_not_require_a_logger_factory()
    {
        var logger = Substitute.For<ILogger<ReplaceDataListItemsHandler>>();
        var services = new ServiceCollection();
        services.AddSingleton(logger);
        services.AddSingleton(Substitute.For<ITenantContext>());
        services.AddSingleton(Substitute.For<IRepository<DataList>>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(logger, scope.ServiceProvider.GetRequiredKeyedService<ILogger>(typeof(ReplaceDataListItemsHandler)));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<ReplaceDataListItemsCommand, Result<DataListDto>>>());
    }

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
