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
    [Theory]
    [InlineData(false, HttpStatusCode.OK)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Admitted_exporter_checks_standing_once_and_needs_edit_to_replace(bool replace, HttpStatusCode expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = ReportingHostWithStanding(out var snapshots);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(admin, ct);
        using var exporter = await RestrictedUserAsync(world, ct);
        snapshots.ClearReceivedCalls();

        // Act
        using var compiled = await exporter.PostAsJsonAsync($"/api/forms/{form}/reporting/compile-schema", new { replace }, ct);

        // Assert
        Assert.Equal(expected, compiled.StatusCode);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submission_download_checks_standing_once_before_sending_the_attachment()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = ReportingHostWithStanding(out var snapshots);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await ExportPreparePermissionFlowTests.CreateFormWithSubmissionAsync(admin, ct);
        var route = $"/api/forms/{form}/submissions/export";
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
        Assert.Contains("export-fixture-answer", await download.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Null(refused.Content.Headers.ContentDisposition);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
    }

    private ReportingWebHost ReportingHostWithStanding(out ICurrentUserAuthorizationService snapshots)
    {
        var authorization = Substitute.For<ICurrentUserAuthorizationService>();
        authorization.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(AdmittedSnapshot()));
        snapshots = authorization;
        return new ReportingWebHost(fixture, services => services.AddTransient(_ => new AccountStanding(authorization)));
    }
}
