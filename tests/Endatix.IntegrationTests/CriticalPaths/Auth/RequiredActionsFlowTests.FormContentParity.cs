using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.FeatureFlows.DataLists;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admitted_stale_settings_retry_returns_current_version_and_checks_standing_once(bool useReplacement)
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData()));
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddScoped(_ => new AccountStanding(snapshots));
            if (useReplacement)
            {
                services.AddSingleton<IFormSettingsRepository>(new SingleFormSettingsStore());
            }
        });
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
        using var read = await owner.GetAsync($"{Route}/settings", ct);
        read.EnsureSuccessStatusCode();
        Assert.Equal("\"1\"", read.Headers.ETag?.Tag);
        var saved = await read.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("1", saved.GetProperty("version").ToString());
        Assert.True(saved.GetProperty("pdf").GetProperty("usesFormTheme").GetBoolean());
    }

    private sealed class SingleFormSettingsStore : IFormSettingsRepository
    {
        private FormSettings? _saved;

        public Task<FormSettings> ReadAsync(Form form, CancellationToken cancellationToken) =>
            Task.FromResult(_saved?.FormId == form.Id && _saved.TenantId == form.TenantId ? _saved : FormSettings.For(form));

        public Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken) =>
            Task.FromResult(_saved?.FormId == formId ? _saved : null);

        public Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken)
        {
            _saved = settings;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Managed_content_requires_ordinary_read_and_edit_grants_before_standing()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        const string form = "1";
        using var user = await RestrictedUserAsync(world, ct);
        using var read = await user.GetAsync($"/api/forms/{form}/definitions", ct);
        using var writeRequest = SettingsChange($"/api/forms/{form}", "\"0\"", true);
        using var write = await user.SendAsync(writeRequest, ct);
        foreach (var response in new[] { read, write })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("requiredActions", await response.Content.ReadAsStringAsync(ct), StringComparison.OrdinalIgnoreCase);
        }
        Assert.Empty(snapshots.ReceivedCalls());
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
