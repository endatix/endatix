using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Submission_download_checks_standing_once_before_sending_the_attachment()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(AdmittedSnapshot()));
        await using var host = new ReportingWebHost(fixture, services =>
            services.AddTransient(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = $"/api/forms/{await CreateFormAsync(admin, ct)}/submissions/export";
        using var exporter = await RestrictedUserAsync(world, ct);
        var csv = new { exportFormat = "csv" };
        snapshots.ClearReceivedCalls();

        // Act
        using var download = await exporter.PostAsJsonAsync(route, csv, ct);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        var pending = AdmittedSnapshot() with
        {
            RequiredActions = [new() { Key = "profile", ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking }]
        };
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(pending));
        snapshots.ClearReceivedCalls();
        using var refused = await exporter.PostAsJsonAsync(route, csv, ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("text/csv", download.Content.Headers.ContentType?.MediaType);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Null(refused.Content.Headers.ContentDisposition);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
    }
}
