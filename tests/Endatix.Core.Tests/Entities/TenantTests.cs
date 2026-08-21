using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Tests.Entities;

public class TenantTests
{
    private const string ValidShortUrl = "xk9mp2qr";

    [Fact]
    public void Constructor_ValidNameAndShortUrl_SetsProperties()
    {
        var tenant = new Tenant("Acme Surveys", ValidShortUrl, "Demo tenant");

        tenant.Name.Should().Be("Acme Surveys");
        tenant.ShortUrl.Should().Be(ValidShortUrl);
        tenant.Description.Should().Be("Demo tenant");
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        var act = () => new Tenant("", ValidShortUrl);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NameDerivedSlug_ThrowsArgumentException()
    {
        var act = () => new Tenant("Acme Regional Surveys", UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys"));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("acme")]           // too short
    [InlineData("XK9MP2QR")]       // uppercase is outside the alphabet
    [InlineData("xk9mp2q-")]       // hyphen is outside the alphabet
    public void Constructor_InvalidShortUrlFormat_ThrowsArgumentException(string shortUrl)
    {
        var act = () => new Tenant("Bad", shortUrl);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateName_ValidName_DoesNotChangeShortUrl()
    {
        var tenant = new Tenant("Acme", ValidShortUrl);

        tenant.UpdateName("Acme Corp");

        tenant.Name.Should().Be("Acme Corp");
        tenant.ShortUrl.Should().Be(ValidShortUrl);
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        var tenant = new Tenant("Acme", ValidShortUrl);

        tenant.UpdateDescription("Updated");

        tenant.Description.Should().Be("Updated");
    }

    [Fact]
    public void RaiseContextChanged_RegistersOutboxEvent()
    {
        var tenant = new Tenant("Acme", ValidShortUrl);
        var occurredAt = DateTime.UtcNow;

        tenant.RaiseContextChanged(7, fromTenantId: 1, toTenantId: tenant.Id, TenantContextChangedEvent.Assumed, occurredAt);

        var domainEvent = tenant.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TenantContextChangedEvent>().Subject;
        domainEvent.Should().BeAssignableTo<IIntegrationEvent>();
        domainEvent.EventType.Should().Be("tenant.context.changed");
        domainEvent.ChangeKind.Should().Be(TenantContextChangedEvent.Assumed);
        domainEvent.ChangeKind.WireValue.Should().Be("assumed");
        domainEvent.ActorUserId.Should().Be(7);
        domainEvent.FromTenantId.Should().Be(1);
        domainEvent.ToTenantId.Should().Be(tenant.Id);
        domainEvent.OccurredAt.Should().Be(occurredAt);
        domainEvent.DateOccurred.Should().Be(occurredAt);

        var payload = domainEvent.GetPayload();
        payload.Should().NotBeNull();
    }
}
