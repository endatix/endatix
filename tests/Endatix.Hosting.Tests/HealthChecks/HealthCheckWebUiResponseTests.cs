using Endatix.Hosting.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Endatix.Hosting.Tests.HealthChecks;

public sealed class HealthCheckWebUiResponseTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, "color: green")]
    [InlineData(HealthStatus.Degraded, "color: orange")]
    [InlineData(HealthStatus.Unhealthy, "color: red")]
    public async Task WebUiWriter_ReportStatus_RendersMatchingColorAndEntryRows(HealthStatus status, string expectedColor)
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new(status, "db description", TimeSpan.FromMilliseconds(5), null, null)
        };
        var report = new HealthReport(entries, status, TimeSpan.FromMilliseconds(5));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var options = HealthCheckOptionsFactory.CreateWebUIOptions();

        // Act
        await options.ResponseWriter!(context, report);

        // Assert
        context.Response.Body.Position = 0;
        var html = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        context.Response.ContentType.Should().Be("text/html");
        html.Should().Contain(expectedColor);
        html.Should().Contain("<td>database</td>");
        html.Should().Contain("db description");
        html.Should().Contain($"class='{status.ToString().ToLower()}'");
    }
}
