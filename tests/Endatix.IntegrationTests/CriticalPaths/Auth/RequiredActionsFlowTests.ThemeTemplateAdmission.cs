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
    public async Task Admitted_template_moves_preserve_no_op_and_locked_folder_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("setup", "/setup") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(action);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var folder = await owner.PostAsJsonAsync("/api/folders", new { name = "Locked originals", immutable = true }, ct);
        folder.EnsureSuccessStatusCode();
        var folderId = (await folder.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString();
        using var created = await owner.PostAsJsonAsync("/api/form-templates", new { name = "Original", folderId, jsonData = "{}" }, ct);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString();
        var path = $"/api/form-templates/{id}";
        using var unchanged = await owner.PatchAsJsonAsync(path, new { folderId }, ct);
        using var clear = await owner.PatchAsJsonAsync(path, new { clearFolderId = true }, ct);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        using var edit = await owner.PatchAsJsonAsync(path, new { description = "Changed" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        Assert.Contains("FOLDER_ASSIGNMENT_UNCHANGED", await unchanged.Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.Conflict, clear.StatusCode);
        await AssertStandingRefusalAsync(edit, await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct), ct);
    }

    [Theory]
    [InlineData("themes")]
    [InlineData("form-templates")]
    public async Task Managed_asset_admission_preserves_ordinary_errors_and_checks_visible_operations_once(string collection)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var neighbor = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var own = await ManagedAsset.CreateAsync(owner, collection, ct);
        var other = await ManagedAsset.CreateAsync(neighbor, collection, ct);
        var page = await owner.GetFromJsonAsync<JsonElement>(own.Path, ct);
        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal(own.Id, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData()));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        using var restricted = await RestrictedUserAsync(world, ct);
        client.DefaultRequestHeaders.Authorization = restricted.DefaultRequestHeaders.Authorization;
        using var denied = await client.GetAsync(own.Read, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        foreach (var target in new[] { other, own with { Id = "9223372036854775807" } })
        {
            foreach (var operation in target.Operations().Where(op => op.Route == target.Detail))
            {
                using var response = await ManagedAsset.SendAsync(client, operation, ct);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
        await AssertAssetAssignmentErrorsAsync(owner, client, own);
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        foreach (var operation in own.Operations().Where(op => op.Route != own.Read || op.Route == own.Detail))
        {
            snapshots.ClearReceivedCalls();
            using var response = await ManagedAsset.SendAsync(client, operation, ct);
            await AssertUnavailableQuestionsAsync(response, ct);
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }
    }

    private static async Task AssertAssetAssignmentErrorsAsync(HttpClient owner, HttpClient client, ManagedAsset target)
    {
        var ct = TestContext.Current.CancellationToken;
        if (target.Collection == "themes")
        {
            var invalidData = new { name = "Shape check", jsonData = "[]" };
            using var invalidCreate = await client.PostAsJsonAsync(target.Path, invalidData, ct);
            using var invalidEdit = await client.PutAsJsonAsync(target.Detail, invalidData, ct);
            using var invalidPatch = await client.PatchAsJsonAsync(target.Detail, invalidData, ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
            Assert.Equal(invalidCreate.StatusCode, invalidEdit.StatusCode);
            Assert.Equal(invalidCreate.StatusCode, invalidPatch.StatusCode);
            var duplicate = new { name = "Another", jsonData = "{}" };
            using var setup = await owner.PostAsJsonAsync(target.Path, duplicate, ct);
            setup.EnsureSuccessStatusCode();
            using var create = await client.PostAsJsonAsync(target.Path, duplicate, ct);
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
            using var patch = await client.PatchAsJsonAsync(target.Detail, duplicate, ct);
            using var put = await client.PutAsJsonAsync(target.Detail, duplicate, ct);
            Assert.Equal(HttpStatusCode.InternalServerError, patch.StatusCode);
            Assert.Equal(patch.StatusCode, put.StatusCode);
            return;
        }
        var absentFolder = new { name = "Moved", jsonData = "{}", folderId = "9223372036854775807" };
        using var insert = await client.PostAsJsonAsync(target.Path, absentFolder, ct);
        using var move = await client.PatchAsJsonAsync(target.Detail, absentFolder, ct);
        Assert.Equal(HttpStatusCode.InternalServerError, insert.StatusCode);
        Assert.Equal(insert.StatusCode, move.StatusCode);
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(true, null)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Managed_assets_allow_notices_no_provider_and_dormant_hosts(bool enforce, RequiredActionSeverity? severity)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (severity.HasValue)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("setup", "/setup") { Severity = severity.Value });
            }
            if (enforce)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        foreach (var collection in new[] { "themes", "form-templates" })
        {
            var asset = await ManagedAsset.CreateAsync(client, collection, ct);
            var warm = await client.GetFromJsonAsync<JsonElement>(asset.Read, ct);
            await asset.AssertResumedAsync(client, warm, ct);
        }
    }
}
