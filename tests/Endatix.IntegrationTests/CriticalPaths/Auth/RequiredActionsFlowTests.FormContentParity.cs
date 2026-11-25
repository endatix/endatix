using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.FeatureFlows.DataLists;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Admitted_stale_settings_retry_returns_current_version_and_checks_standing_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData()));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var (Route, Definition) = await ContentTargetAsync(owner, ct);
        foreach (var (value, expected) in new[] { (true, HttpStatusCode.OK), (true, HttpStatusCode.OK), (false, HttpStatusCode.PreconditionFailed) })
        {
            snapshots.ClearReceivedCalls();
            using var request = SettingsChange(Route, "\"0\"", value);
            using var response = await owner.SendAsync(request, ct);
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("1", body.GetProperty("version").ToString());
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }
        var saved = await owner.GetFromJsonAsync<JsonElement>($"{Route}/settings", ct);
        Assert.True(saved.GetProperty("pdf").GetProperty("usesFormTheme").GetBoolean());
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(true, null)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Managed_content_allows_notices_empty_and_dormant_standing(bool enforced, RequiredActionSeverity? severity)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (severity.HasValue)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("terms", "/terms") { Severity = severity.Value });
            }
            if (enforced)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var (Route, Definition) = await ContentTargetAsync(owner, ct);
        await AssertContentSuccessAsync(owner, ContentRequests(Route, Definition), ct);
    }

    [Fact]
    public async Task A_public_respondent_reads_the_definition_and_obtains_a_form_token_without_account_standing()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var (form, list) = await FormAccessTokenUseFlowTests.SeedRuntimeFormAsync(world, true, ct);
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        using var anonymous = world.AnonymousClient();
        var definition = await anonymous.GetFromJsonAsync<JsonElement>($"/api/forms/{form}/definition", ct);
        Assert.Equal(form.ToString(System.Globalization.CultureInfo.InvariantCulture), definition.GetProperty("formId").GetString());
        using var respondent = await PublicDataListScopeFlowTests.AnonymousWithFormTokenAsync(world, form, ct);
        var choices = await respondent.GetFromJsonAsync<JsonElement>(PublicDataListScopeFlowTests.SearchUrl(form, list), ct);
        Assert.Equal("bound-choice", Assert.Single(choices.GetProperty("items").EnumerateArray()).GetProperty("value").GetString());
        Assert.Empty(snapshots.ReceivedCalls());
    }
}
