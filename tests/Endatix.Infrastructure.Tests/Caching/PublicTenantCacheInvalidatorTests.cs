using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Caching;

public sealed class PublicTenantCacheInvalidatorTests
{
    private const long TENANT_ID = 42;
    private const string SHORT_URL = "xk9mp2qr";

    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IRepository<Tenant> _tenants = Substitute.For<IRepository<Tenant>>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly PublicTenantCacheInvalidator _invalidator;

    public PublicTenantCacheInvalidatorTests()
    {
        _tenantContext.TenantId.Returns(TENANT_ID);
        _tenants.SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new Tenant("Acme", SHORT_URL, "Primary") { Id = TENANT_ID });
        _invalidator = new PublicTenantCacheInvalidator(_tenantContext, _tenants, _cache, NullLogger<PublicTenantCacheInvalidator>.Instance);
    }

    [Fact]
    public async Task InvalidateCurrentTenantAsync_RemovesTheEntryForTheTenantsShortUrl()
    {
        // Act
        await _invalidator.InvalidateCurrentTenantAsync(TestContext.Current.CancellationToken);

        // Assert
        await _cache.Received(1).RemoveAsync(PublicTenantCacheKeys.Entry(SHORT_URL), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateCurrentTenantAsync_WhenTheCacheThrows_DoesNotRethrow()
    {
        // Arrange
        _cache.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("Redis unavailable")));

        // Act
        var act = () => _invalidator.InvalidateCurrentTenantAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
