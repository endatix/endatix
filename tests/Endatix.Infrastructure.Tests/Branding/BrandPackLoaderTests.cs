using Endatix.Core.Branding;
using Endatix.Infrastructure.Branding;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Tests.Branding;

public sealed class BrandPackLoaderTests : IDisposable
{
    private readonly string _packDirectory = Path.Combine(Path.GetTempPath(), $"brand-pack-{Guid.NewGuid():N}");

    public BrandPackLoaderTests() => Directory.CreateDirectory(_packDirectory);

    public void Dispose() => Directory.Delete(_packDirectory, recursive: true);

    public static TheoryData<string> ValidFixtures() => [.. FixtureNames("valid-")];

    public static TheoryData<string, string> InvalidFixtures() => new()
    {
        { "invalid-version.json", "/version" },
        { "invalid-support-mailto.json", "/links/support" },
    };

    // Invalid in fields only the Hub reads (assets, unknown keys); the Hub rejects these packs at its own startup.
    public static TheoryData<string> HubOnlyInvalidFixtures() =>
    [
        "invalid-path-traversal.json",
        "invalid-unknown-key.json",
    ];

    public static TheoryData<string, string> InvalidPacks() => new()
    {
        { """{ "version": 1, "name": "   " }""", "/name: must not be blank" },
        { """{ "version": 1, "name": "Example\u0007Forms" }""", "/name: must not contain control characters" },
        { $$"""{ "version": 1, "name": "{{new string('x', 61)}}" }""", "/name: must be at most 60 characters" },
        { """{ "version": 1, "name": "Example Forms", "links": { "website": "ftp://example.com" } }""", "/links/website: must be an http, https or mailto URL" },
        { """{ "version": 1, "name": "Example Forms", "links": { "support": "ftp://example.com" } }""", "/links/support: must be an http, https or mailto URL" },
        { """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:?subject=Help" } }""", "/links/support: must be a mailto link to one email address" },
        { """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:not-an-email" } }""", "/links/support: must be a mailto link to one email address" },
        { """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:Example <help@example.com>" } }""", "/links/support:" },
        { """{ "version": 1, "name": "Example Forms", "links": "https://example.com" }""", "/links: must be a JSON object" },
        { """{ "version": 1, "name": 7 }""", "/name: must be a string" },
    };

    [Fact]
    public void InvalidFixtures_CoverEveryInvalidFixtureFile()
    {
        // Arrange
        var covered = InvalidFixtures().Select(row => row.Data.Item1)
            .Concat(HubOnlyInvalidFixtures().Select(row => row.Data));

        // Act
        var onDisk = FixtureNames("invalid-");

        // Assert
        covered.Should().BeEquivalentTo(onDisk);
    }

    [Fact]
    public void Load_FlagOffWithInvalidPack_ReturnsBuiltInBrand()
    {
        // Arrange
        UsePackFixture("invalid-unknown-key.json");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "false"));

        // Assert
        brand.Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Fact]
    public void Load_FlagMissingWithValidPack_ReturnsBuiltInBrand()
    {
        // Arrange
        UsePackFixture("valid-full.json");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: null));

        // Assert
        brand.Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Fact]
    public void Load_FlagOnWithoutBrandDirectory_ReturnsBuiltInBrand()
    {
        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true", brandDirectory: null));

        // Assert
        brand.Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Theory]
    [MemberData(nameof(ValidFixtures))]
    public void Load_FlagOnWithValidFixture_ReturnsPackBrand(string fixture)
    {
        // Arrange
        UsePackFixture(fixture);

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.ProductName.Should().Be("Example Forms");
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void Load_FlagOnWithInvalidFixture_ThrowsNamingTheFieldPath(string fixture, string fieldPath)
    {
        // Arrange
        UsePackFixture(fixture);

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message.Should().Contain(fieldPath);
    }

    [Theory]
    [MemberData(nameof(HubOnlyInvalidFixtures))]
    public void Load_FixtureInvalidOnlyInHubFields_LoadsThePack(string fixture)
    {
        // Arrange
        UsePackFixture(fixture);

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.ProductName.Should().Be("Example Forms");
    }

    [Theory]
    [MemberData(nameof(InvalidPacks))]
    public void Load_InvalidFieldTheApiReads_ThrowsNamingIt(string pack, string failure)
    {
        // Arrange
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), pack);

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message.Should().Contain(failure);
    }

    [Fact]
    public void Load_RootAndFieldFailures_NamesBoth()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), """{ "version": 2 }""");

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message
            .Should().Contain("/name: is required").And.Contain("/version: must be 1");
    }

    [Fact]
    public void Load_FullPack_MapsEveryFieldTheApiReads()
    {
        // Arrange
        UsePackFixture("valid-full.json");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.Should().BeEquivalentTo(new DeploymentBrand(
            ProductName: "Example Forms",
            AppName: "Example Forms",
            AssistantName: "Example Assistant",
            WebsiteUrl: "https://example.com",
            SupportAddress: "help@example.com"));
    }

    [Theory]
    [InlineData("mailto:help@example.com?subject=Help", "help@example.com")]
    [InlineData("mailto:help@example.com#contact", "help@example.com")]
    [InlineData("mailto:first%2Blast@example.com", "first+last@example.com")]
    [InlineData("mailto:help%40example.com", "help@example.com")]
    [InlineData("https://example.com/support", "support@endatix.com")]
    public void Load_SupportLink_GivesTheSupportAddressOnlyForMailto(string supportLink, string supportAddress)
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            $$"""{ "version": 1, "name": "Example Forms", "links": { "support": "{{supportLink}}" } }""");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.SupportAddress.Should().Be(supportAddress);
    }

    [Fact]
    public void Load_NameOf60SupplementaryCharacters_LoadsBecauseTheLimitCountsCodePoints()
    {
        // Arrange: each emoji is one code point and two UTF-16 units.
        var name = Emoji(60);
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), $$"""{ "version": 1, "name": "{{name}}" }""");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.ProductName.Should().Be(name);
    }

    [Fact]
    public void Load_NameOf61SupplementaryCharacters_ThrowsNamingTheLimit()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), $$"""{ "version": 1, "name": "{{Emoji(61)}}" }""");

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message.Should().Contain("/name: must be at most 60 characters");
    }

    [Fact]
    public void Load_MailtoListOfAddresses_KeepsTheBuiltInSupportAddressAndWarns()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:a@example.com,b@example.com" } }""");
        List<string> warnings = [];

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"), warnings);

        // Assert
        brand.SupportAddress.Should().Be("support@endatix.com");
        warnings.Should().ContainSingle().Which.Should().Contain("/links/support").And.Contain("more than one address");
    }

    [Fact]
    public void Load_WebSupportLink_KeepsTheBuiltInSupportAddressAndWarns()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            """{ "version": 1, "name": "Example Forms", "links": { "support": "https://forms.example.com/help" } }""");
        List<string> warnings = [];

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"), warnings);

        // Assert
        brand.SupportAddress.Should().Be("support@endatix.com");
        warnings.Should().ContainSingle().Which.Should().Contain("/links/support").And.Contain("not a mailto link");
    }

    [Fact]
    public void Load_MailtoSupportLink_DoesNotWarn()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:help@example.com" } }""");
        List<string> warnings = [];

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"), warnings);

        // Assert
        brand.SupportAddress.Should().Be("help@example.com");
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Load_PackWithAnEmailSection_IgnoresIt()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            """{ "version": 1, "name": "Example Forms", "email": { "fromAddress": "not-an-email" } }""");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.Should().Be(DeploymentBrand.BuiltIn with { ProductName = "Example Forms", AppName = "Example Forms" });
    }

    [Fact]
    public void Load_MinimalPack_KeepsBuiltInValues()
    {
        // Arrange
        UsePackFixture("valid-minimal.json");

        // Act
        var brand = BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        brand.Should().BeEquivalentTo(DeploymentBrand.BuiltIn with
        {
            ProductName = "Example Forms",
            AppName = "Example Forms",
        });
    }

    [Fact]
    public void Load_DirectoryWithoutBrandJson_ThrowsNamingTheFile()
    {
        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message.Should().Contain("brand.json");
    }

    [Fact]
    public void Load_MalformedJson_ThrowsBrandPackException()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), "{ \"version\": 1, ");

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        load.Should().Throw<BrandPackException>().Which.Message.Should().Contain("not valid JSON");
    }

    private static string Emoji(int count) => string.Concat(Enumerable.Repeat("\U0001F600", count));

    private void UsePackFixture(string fixture) =>
        File.Copy(FixturePath(fixture), Path.Combine(_packDirectory, "brand.json"));

    private IConfiguration Configuration(string? flag, string? brandDirectory = "") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:FeatureFlags:WhiteLabeling"] = flag,
                [BrandPackLoader.BrandDirectoryKey] = brandDirectory == "" ? _packDirectory : brandDirectory,
            })
            .Build();

    private static string FixturePath(string fixture) =>
        Path.Combine(AppContext.BaseDirectory, "Branding", "Fixtures", fixture);

    private static IEnumerable<string> FixtureNames(string prefix) =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Branding", "Fixtures"), $"{prefix}*.json")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);
}
