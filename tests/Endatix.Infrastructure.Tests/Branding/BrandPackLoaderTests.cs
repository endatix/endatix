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
        { "invalid-path-traversal.json", "/assets/icon" },
        { "invalid-unknown-key.json", "/fonts" },
        { "invalid-version.json", "/version" },
        { "invalid-email-format.json", "/email/fromAddress" },
    };

    [Fact]
    public void InvalidFixtures_CoverEveryInvalidFixtureFile()
    {
        // Arrange
        var covered = InvalidFixtures().Select(row => row.Data.Item1);

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

    [Fact]
    public void Load_InvalidNestedField_NamesOnlyThatField()
    {
        // Arrange
        UsePackFixture("invalid-path-traversal.json");

        // Act
        var load = () => BrandPackLoader.Load(Configuration(flag: "true"));

        // Assert
        var message = load.Should().Throw<BrandPackException>().Which.Message;
        message.Should().Contain("/assets/icon:").And.NotContain("/assets:").And.NotContain(" /:");
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
            .Should().Contain("/:").And.Contain("name").And.Contain("/version:");
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
            Email: new BrandEmail(
                SenderName: "Example Forms",
                FromAddress: "noreply@example.com",
                SupportAddress: "help@example.com")));
    }

    [Fact]
    public void Load_MinimalPack_KeepsBuiltInValuesAndSendsAsTheProductName()
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
            Email = DeploymentBrand.BuiltIn.Email with { SenderName = "Example Forms" },
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
