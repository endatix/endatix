extern alias EndatixWebHost;

using Endatix.Core.Branding;
using Endatix.Infrastructure.Branding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The real host reads the brand pack while it starts: an invalid pack with the white-labeling flag on stops it, and
/// with the flag off a mounted pack is ignored.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class BrandPackStartupTests(EndatixIntegrationWebHostFixture fixture) : IDisposable
{
    private const string InvalidPack = """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:not-an-email" } }""";
    private const string ValidPack = """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:help@example.com" } }""";

    private readonly string _packDirectory = Directory.CreateTempSubdirectory("brand-pack-").FullName;

    public void Dispose() => Directory.Delete(_packDirectory, recursive: true);

    [Fact]
    public async Task Invalid_brand_pack_with_the_flag_on_stops_startup_naming_the_field()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_packDirectory, "brand.json"), InvalidPack, TestContext.Current.CancellationToken);
        await using var factory = HostWithPack(whiteLabeling: true);

        // Act
        var error = Record.Exception(() => factory.Services);

        // Assert
        var brandPackError = FindBrandPackError(error);
        brandPackError.Should().NotBeNull("an invalid pack stops startup");
        brandPackError!.Message.Should().Contain("/links/support");
    }

    [Fact]
    public async Task Invalid_brand_pack_with_the_flag_off_is_ignored()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_packDirectory, "brand.json"), InvalidPack, TestContext.Current.CancellationToken);
        await using var factory = HostWithPack(whiteLabeling: false);

        // Act
        var brand = factory.Services.GetRequiredService<DeploymentBrand>();

        // Assert
        brand.Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Fact]
    public async Task Valid_brand_pack_with_the_flag_on_is_the_deployment_brand()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_packDirectory, "brand.json"), ValidPack, TestContext.Current.CancellationToken);
        await using var factory = HostWithPack(whiteLabeling: true);

        // Act
        var brand = factory.Services.GetRequiredService<DeploymentBrand>();

        // Assert
        brand.ProductName.Should().Be("Example Forms");
        brand.SupportAddress.Should().Be("help@example.com");
    }

    private WebApplicationFactory<EndatixWebHost::Program> HostWithPack(bool whiteLabeling) =>
        new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder
                .UseSetting("Endatix:FeatureFlags:WhiteLabeling", whiteLabeling.ToString())
                .UseSetting(BrandPackLoader.BrandDirectoryKey, _packDirectory));

    private static BrandPackException? FindBrandPackError(Exception? error) => error switch
    {
        null => null,
        BrandPackException brandPack => brandPack,
        AggregateException aggregate => aggregate.InnerExceptions.Select(FindBrandPackError).FirstOrDefault(found => found != null),
        _ => FindBrandPackError(error.InnerException),
    };
}
