using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.Search;

public sealed class SearchManagedDataListItemsHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_WithoutPositiveTenant_DoesNotDispatchSearch(long tenantId)
    {
        // Arrange
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);
        var lists = Substitute.For<IRepository<DataList>>();
        var sender = Substitute.For<ISender>();
        var handler = new SearchManagedDataListItemsHandler(new DataListVisibility(tenant, lists), sender);

        // Act
        var result = await handler.Handle(new(new(1, null, 0, 10)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Empty(lists.ReceivedCalls());
        Assert.Empty(sender.ReceivedCalls());
    }
}
