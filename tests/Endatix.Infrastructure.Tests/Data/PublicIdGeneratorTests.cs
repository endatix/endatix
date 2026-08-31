using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Data;

public class PublicIdGeneratorTests
{
    private readonly PublicIdGenerator _sut = new();

    [Fact]
    public void Create_ShortSlug_ReturnsShortSlugLengthAlphabetCharacters()
    {
        var value = _sut.Create(PublicIdKind.ShortSlug);

        value.Should().HaveLength(PublicId.ShortSlugLength);
        PublicId.IsValidShortSlug(value).Should().BeTrue();
    }

    [Fact]
    public void Create_ShortSlug_PrefersLetterHeavyIds()
    {
        for (var i = 0; i < 50; i++)
        {
            var value = _sut.Create(PublicIdKind.ShortSlug);

            PublicId.IsValidShortSlug(value).Should().BeTrue();
            PublicId.IsLetterHeavy(value).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_ShortSlug_IsAlreadyNormalized()
    {
        var value = _sut.Create(PublicIdKind.ShortSlug);

        // Lowercase-only output is what lets the unique index behave the same on
        // PostgreSQL and on SQL Server's default case-insensitive collation.
        value.Should().Be(value.ToLowerInvariant());
        PublicId.Normalize(value).Should().Be(value);
    }

    [Fact]
    public void Create_ShortSlug_DoesNotMatchNameDerivedSlug()
    {
        var value = _sut.Create(PublicIdKind.ShortSlug);

        value.Should().NotBe(UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys"));
    }

    [Fact]
    public void Create_ShortSlug_RepeatedDrawsAreUsuallyUnique()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < 200; i++)
        {
            seen.Add(_sut.Create(PublicIdKind.ShortSlug)).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_UnsupportedKind_Throws()
    {
        var act = () => _sut.Create((PublicIdKind)int.MaxValue);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }
}
