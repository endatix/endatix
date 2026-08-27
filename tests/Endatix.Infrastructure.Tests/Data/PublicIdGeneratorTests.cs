using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Data;

public class PublicIdGeneratorTests
{
    private readonly PublicIdGenerator _sut = new();

    [Fact]
    public void Create_Tenant_ReturnsEightAlphabetCharacters()
    {
        var value = _sut.Create(PublicIdKind.Tenant);

        value.Should().HaveLength(PublicId.TenantLength);
        PublicId.IsValidTenantSlug(value).Should().BeTrue();
    }

    [Fact]
    public void Create_Tenant_PrefersLetterHeavyIds()
    {
        for (var i = 0; i < 50; i++)
        {
            var value = _sut.Create(PublicIdKind.Tenant);

            PublicId.IsValidTenantSlug(value).Should().BeTrue();
            PublicId.IsLetterHeavy(value).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_Tenant_DoesNotMatchNameDerivedSlug()
    {
        var value = _sut.Create(PublicIdKind.Tenant);

        value.Should().NotBe(UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys"));
    }

    [Fact]
    public void Create_Tenant_RepeatedDrawsAreUsuallyUnique()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < 200; i++)
        {
            seen.Add(_sut.Create(PublicIdKind.Tenant)).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_UnsupportedKind_Throws()
    {
        var act = () => _sut.Create((PublicIdKind)int.MaxValue);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }
}
