using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Managed_form_content_resumes_on_the_same_session_after_clearance()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("terms", "/terms") { Pending = false };
        var commands = new ContentCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddSingleton<IRequiredActionProvider>(action);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = $"/api/forms/{await CreateFormAsync(owner, ct)}";
        var list = await owner.GetFromJsonAsync<JsonElement>($"{route}/definitions", ct);
        var id = list.GetProperty("items")[0].GetProperty("id").GetString();
        using var firstSettings = SettingsChange(route, "\"0\"", true);
        using var seeded = await owner.SendAsync(firstSettings, ct);
        Assert.Equal("\"1\"", seeded.Headers.ETag?.Tag);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.True(commands.Count > 0);
        commands.Count = 0;
        foreach (var request in ContentRequests(route, id!))
        {
            using var sent = request;
            using var refused = await owner.SendAsync(sent, ct);
            await AssertStandingRefusalAsync(refused, me, ct);
        }
        Assert.Equal(0, commands.Count);
        action.Pending = false;
        await InvalidateAsync(world, ct);
        var recovered = await owner.GetFromJsonAsync<JsonElement>($"{route}/definitions", ct);
        Assert.True(JsonElement.DeepEquals(list, recovered));
        using var settings = await owner.GetAsync($"{route}/settings", ct);
        Assert.Equal("\"1\"", settings.Headers.ETag?.Tag);
        await AssertContentSuccessAsync(owner, ContentRequests(route, id!), ct);
    }

    private static async Task AssertContentSuccessAsync(HttpClient owner, IEnumerable<HttpRequestMessage> requests, CancellationToken ct)
    {
        foreach (var request in requests)
        {
            using var sent = request;
            using var resumed = await owner.SendAsync(sent, ct);
            Assert.Equal(sent.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, resumed.StatusCode);
        }
    }

    [Fact]
    public async Task Managed_content_unavailability_follows_ordinary_target_and_request_checks()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        var commands = new ContentCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddScoped(_ => new AccountStanding(snapshots));
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var (Route, Definition) = await ContentTargetAsync(owner, ct);
        var hidden = await ContentTargetAsync(other, ct);
        commands.Count = 0;
        await AssertContentStatusAsync(owner, ContentRequests(hidden.Route, hidden.Definition), HttpStatusCode.NotFound);
        await AssertContentStatusAsync(owner, [SettingsChange(hidden.Route, null, true)], HttpStatusCode.PreconditionRequired);
        await AssertContentStatusAsync(owner, [SettingsChange(hidden.Route, "bad", true)], HttpStatusCode.BadRequest);
        await AssertContentStatusAsync(owner, [SettingsChange("/api/forms/9223372036854775807", "\"0\"", true)], HttpStatusCode.NotFound);
        using var missingDefinition = await owner.GetAsync($"{Route}/definitions/{hidden.Definition}", ct);
        Assert.Equal(HttpStatusCode.NotFound, missingDefinition.StatusCode);
        using var empty = await owner.GetAsync($"{Route}/definitions?createdTo=2000-01-01", ct);
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());
        await AssertContentStatusAsync(owner, ContentRequests(Route, Definition), HttpStatusCode.ServiceUnavailable);
        await snapshots.Received(10).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        Assert.Equal(0, commands.Count);
    }

    private static async Task<(string Route, string Definition)> ContentTargetAsync(HttpClient client, CancellationToken ct)
    {
        var route = $"/api/forms/{await CreateFormAsync(client, ct)}";
        var definition = await client.GetFromJsonAsync<JsonElement>($"{route}/definition", ct);
        return (route, definition.GetProperty("id").GetString()!);
    }

    private static async Task AssertContentStatusAsync(HttpClient client, IEnumerable<HttpRequestMessage> requests,
        HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var request in requests)
        {
            using var sent = request;
            using var response = await client.SendAsync(sent, ct);
            Assert.True(response.StatusCode == status, Operation(response));
            if (status == HttpStatusCode.ServiceUnavailable)
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                Assert.Contains("Try again", problem.GetProperty("detail").GetString());
            }
        }
    }

    private sealed class ContentCommands : DbCommandInterceptor
    {
        internal int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("PdfJson", StringComparison.OrdinalIgnoreCase) || sql.Contains("WebHookSettingsJson", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("JsonData", StringComparison.OrdinalIgnoreCase) || sql.Contains("FormSettings", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase) || sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase))
            {
                Count++;
            }
            return ValueTask.FromResult(result);
        }
    }

    private static IEnumerable<HttpRequestMessage> ContentRequests(string route, string id)
    {
        var change = new { isDraft = true, jsonData = """{"pages":[]}""" };
        var active = $"{route}/definition";
        var list = $"{route}/definitions";
        var byId = $"{list}/{id}";
        yield return new(HttpMethod.Get, $"{active}/fields");
        yield return new(HttpMethod.Get, list);
        yield return new(HttpMethod.Get, byId);
        yield return new(HttpMethod.Get, $"{route}/settings");
        yield return new(HttpMethod.Patch, active) { Content = JsonContent.Create(change) };
        yield return new(HttpMethod.Patch, byId) { Content = JsonContent.Create(change) };
        yield return SettingsChange(route, "\"0\"", true);
        yield return new(HttpMethod.Post, list) { Content = JsonContent.Create(change) };
        yield return new(HttpMethod.Put, active) { Content = JsonContent.Create(change) };
        yield return new(HttpMethod.Put, byId) { Content = JsonContent.Create(change) };
    }

    private static HttpRequestMessage SettingsChange(string route, string? version, bool usesTheme)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{route}/settings")
        {
            Content = JsonContent.Create(new { pdf = new { usesFormTheme = usesTheme } })
        };
        if (version is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", version);
        }
        return request;
    }
}
