using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Data;

public class ShortUrlGeneratorTests
{
    private readonly ShortUrlGenerator _sut = new();

    [Fact]
    public void Create_Standard_ReturnsStandardLengthAlphabetCharacters()
    {
        var value = _sut.Create(ShortUrlKind.Standard);

        value.Should().HaveLength(ShortUrl.StandardLength);
        ShortUrl.IsValid(value).Should().BeTrue();
    }

    [Fact]
    public void Create_Standard_PrefersLetterHeavyIds()
    {
        for (var i = 0; i < 50; i++)
        {
            var value = _sut.Create(ShortUrlKind.Standard);

            ShortUrl.IsValid(value).Should().BeTrue();
            ShortUrl.IsLetterHeavy(value).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_Standard_IsAlreadyNormalized()
    {
        var value = _sut.Create(ShortUrlKind.Standard);

        // Lowercase-only output is what lets the unique index behave the same on
        // PostgreSQL and on SQL Server's default case-insensitive collation.
        value.Should().Be(value.ToLowerInvariant());
        ShortUrl.Normalize(value).Should().Be(value);
    }

    [Fact]
    public void Create_Standard_DoesNotMatchNameDerivedSlug()
    {
        var value = _sut.Create(ShortUrlKind.Standard);

        value.Should().NotBe(UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys"));
    }

    [Fact]
    public void Create_Standard_RepeatedDrawsAreUsuallyUnique()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < 200; i++)
        {
            seen.Add(_sut.Create(ShortUrlKind.Standard)).Should().BeTrue();
        }
    }

    [Fact]
    public void Create_UnsupportedKind_Throws()
    {
        var act = () => _sut.Create((ShortUrlKind)int.MaxValue);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }
}
