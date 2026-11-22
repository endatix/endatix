using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Core.UseCases.TenantBranding.History;
using Endatix.Core.Versioning;
using TenantBrandingEntity = Endatix.Core.Entities.TenantBranding;

namespace Endatix.Core.Tests.UseCases.TenantBranding;

public sealed class TenantBrandingReadHandlerTests
{
    private const long TENANT_ID = 42;
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";

    private static readonly DateTime _publishedAt = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITenantBrandingRepository _repository = Substitute.For<ITenantBrandingRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ITenantEntitlements _entitlements = Substitute.For<ITenantEntitlements>();

    public TenantBrandingReadHandlerTests()
    {
        _tenantContext.TenantId.Returns(TENANT_ID);
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(true);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_NothingPublished_ReturnsTheUnsavedVersion()
    {
        // Act
        var result = await GetHandler().Handle(new GetTenantBrandingQuery(), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entitled.Should().BeTrue();
        result.Value.Branding.Should().Be(new TenantBrandingDto(null, null, 0, null));
    }

    [Fact]
    public async Task Get_Published_ReturnsTheLiveVersion()
    {
        // Arrange
        _repository.FindAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns(PublishedBlue());

        // Act
        var result = await GetHandler().Handle(new GetTenantBrandingQuery(), Cancellation);

        // Assert
        result.Value.Entitled.Should().BeTrue();
        result.Value.Branding.PublishedJson.Should().Contain("#0052d1");
        result.Value.Branding.PublishedNumber.Should().Be(1);
        result.Value.Branding.Version.Should().Be(1);
        result.Value.Branding.PublishedAt.Should().Be(_publishedAt);
    }

    [Fact]
    public async Task Get_NotEntitled_HidesTheBrandButKeepsTheVersion()
    {
        // Arrange
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(false);
        _repository.FindAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns(PublishedBlue());

        // Act
        var result = await GetHandler().Handle(new GetTenantBrandingQuery(), Cancellation);

        // Assert
        result.Value.Entitled.Should().BeFalse();
        result.Value.Branding.Should().Be(new TenantBrandingDto(null, null, 1, null));
    }

    [Fact]
    public async Task Get_NoTenant_IsUnauthorized()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0);

        // Act
        var result = await GetHandler().Handle(new GetTenantBrandingQuery(), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task History_ReturnsNewestFirstWithPaging()
    {
        // Arrange
        TenantBrandingVersion[] newestFirst = [Version(3, restoredFrom: 1), Version(2)];
        _repository.CountVersionsAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns(3);
        _repository.ListVersionsAsync(TENANT_ID, Arg.Is<ResolvedPage>(page => page.Skip == 0 && page.PageSize == 2), Arg.Any<CancellationToken>())
            .Returns(newestFirst);

        // Act
        var result = await HistoryHandler().Handle(new ListTenantBrandingVersionsQuery(1, 2), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRecords.Should().Be(3);
        result.Value.TotalPages.Should().Be(2);
        result.Value.Items.Should().Equal(
            new TenantBrandingVersionDto(3, _publishedAt, 7, ContentVersionSources.RESTORE, 1),
            new TenantBrandingVersionDto(2, _publishedAt, 7, ContentVersionSources.PUBLISH, null));
    }

    [Fact]
    public async Task History_NotEntitled_IsEmpty()
    {
        // Arrange
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CountVersionsAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns(3);

        // Act
        var result = await HistoryHandler().Handle(new ListTenantBrandingVersionsQuery(null, null), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalRecords.Should().Be(0);
    }

    private GetTenantBrandingHandler GetHandler() => new(_repository, _tenantContext, _entitlements);

    private ListTenantBrandingVersionsHandler HistoryHandler() => new(_repository, _tenantContext, _entitlements);

    private static TenantBrandingEntity PublishedBlue()
    {
        var branding = new TenantBrandingEntity(TENANT_ID);
        branding.Publish(BrandOverride.Parse(BLUE, TENANT_ID).Value, 0, new ContentVersionStamp(_publishedAt, 7));
        return branding;
    }

    private static TenantBrandingVersion Version(long number, long? restoredFrom = null) => new()
    {
        TenantId = TENANT_ID,
        Number = number,
        Json = BLUE,
        ContentHash = "hash",
        CreatedAt = _publishedAt,
        CreatedBy = 7,
        Source = restoredFrom is null ? ContentVersionSources.PUBLISH : ContentVersionSources.RESTORE,
        RestoredFromNumber = restoredFrom,
    };
}
