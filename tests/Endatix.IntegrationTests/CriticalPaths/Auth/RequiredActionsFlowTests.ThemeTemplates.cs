using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData("themes")]
    [InlineData("form-templates")]
    public async Task Pending_owner_cannot_use_managed_theme_or_template_operations(string collection)
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("setup", "/setup") { Pending = false };
        var conflict = new MutableProvider("other", "/other") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(action);
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var target = await ManagedAsset.CreateAsync(client, collection, ct);
        var warm = await client.GetFromJsonAsync<JsonElement>(target.Read, ct);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        foreach (var operation in target.Operations())
        {
            using var response = await ManagedAsset.SendAsync(client, operation, ct);
            await AssertStandingRefusalAsync(response, me, ct);
        }
        conflict.Pending = true;
        await InvalidateAsync(world, ct);
        foreach (var operation in target.Operations())
        {
            using var response = await ManagedAsset.SendAsync(client, operation, ct);
            await AssertUnavailableQuestionsAsync(response, ct);
        }
        action.Pending = conflict.Pending = false;
        await InvalidateAsync(world, ct);
        await target.AssertResumedAsync(client, warm, ct);
    }

    [Fact]
    public async Task Template_permissions_describe_the_caller_for_existing_and_missing_ids()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var template = await ManagedAsset.CreateAsync(client, "form-templates", ct);
        var existing = await client.GetFromJsonAsync<JsonElement>(template.Read, ct);
        var absent = await client.GetFromJsonAsync<JsonElement>("/api/access/form-templates/9223372036854775807", ct);
        Assert.Equal("9223372036854775807", absent.GetProperty("templateId").GetString());
        Assert.True(JsonElement.DeepEquals(existing.GetProperty("permissions"), absent.GetProperty("permissions")));
    }

    private sealed record ManagedAsset(string Collection, string Id)
    {
        internal string Path => $"/api/{Collection}";
        internal string Detail => $"{Path}/{Id}";
        internal string Read => Collection == "themes" ? Detail : $"/api/access/form-templates/{Id}";
        internal static async Task<ManagedAsset> CreateAsync(HttpClient client, string collection, CancellationToken ct)
        {
            using var created = await client.PostAsJsonAsync($"/api/{collection}", new { name = "Original", jsonData = "{}" }, ct);
            created.EnsureSuccessStatusCode();
            return new(collection, (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!);
        }

        internal (string Method, string Route)[] Operations() => [("GET", Path), ("GET", Detail), ("POST", Path),
            ("PATCH", Detail), (Collection == "themes" ? "PUT" : "GET", Read), ("DELETE", Detail)];

        internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, (string Method, string Route) operation, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(new HttpMethod(operation.Method), operation.Route);
            if (operation.Method is "POST" or "PATCH" or "PUT")
            {
                request.Content = JsonContent.Create(new { name = operation.Method == "POST" ? "New asset" : "Changed", jsonData = "{}" });
            }
            return await client.SendAsync(request, ct);
        }

        internal async Task AssertResumedAsync(HttpClient client, JsonElement warm, CancellationToken ct)
        {
            var recovered = await client.GetFromJsonAsync<JsonElement>(Detail, ct);
            Assert.Equal("Original", recovered.GetProperty("name").GetString());
            var read = await client.GetFromJsonAsync<JsonElement>(Read, ct);
            if (Collection == "form-templates")
            {
                Assert.Equal(warm.GetProperty("eTag").GetString(), read.GetProperty("eTag").GetString());
            }
            else
            {
                Assert.True(JsonElement.DeepEquals(warm, read));
            }
            foreach (var operation in Operations())
            {
                using var response = await SendAsync(client, operation, ct);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
