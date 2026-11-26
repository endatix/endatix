using System.Net;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed class EmbedHostRegistrationTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task Anonymous_embed_playground_follows_the_hosts_middleware_configuration(bool enabled, HttpStatusCode expected)
    {
        // Arrange
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder => builder.UseSetting("Endatix:DevTools:EmbedHost:Enabled", enabled.ToString()));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        using var page = await client.GetAsync("/dev/embed-host", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, page.StatusCode);
        if (enabled)
        {
            Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
            Assert.Contains("formId", await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }
}
