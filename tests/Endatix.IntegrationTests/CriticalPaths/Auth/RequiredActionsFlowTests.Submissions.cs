using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Managed_submission_refusals_precede_payload_storage_and_writes()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("terms", "/terms") { Pending = false };
        var commands = new ContentCommands();
        var files = Substitute.For<ISubmissionFileExtractor>();
        files.ExtractFilesAsync(default, 0, 0, "", ct).ReturnsForAnyArgs(_ =>
            [new("sample.txt", "text/plain", new MemoryStream([42]))]);
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddSingleton<IRequiredActionProvider>(action);
            services.AddSingleton(files);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(owner, ct);
        var collection = $"/api/forms/{form}/submissions";
        using var created = await owner.PostAsJsonAsync(collection, new { jsonData = """{"a":"sample"}""" }, ct);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString();
        var access = $"/api/access/forms/{form}/submissions/{id}";
        using var warm = await owner.GetAsync(access, ct);
        warm.EnsureSuccessStatusCode();
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        commands.Count = 0;
        foreach (var request in SubmissionRequests(collection, $"{collection}/{id}", access))
        {
            using var sent = request;
            using var response = await owner.SendAsync(sent, ct);
            await AssertStandingRefusalAsync(response, me, ct);
        }
        Assert.Equal(0, commands.Count);
        Assert.Empty(files.ReceivedCalls());

        action.Pending = false;
        await InvalidateAsync(world, ct);
        using var download = await owner.PostAsJsonAsync($"{collection}/{id}/files", new { }, ct);
        download.EnsureSuccessStatusCode();
        using var archive = new ZipArchive(await download.Content.ReadAsStreamAsync(ct));
        Assert.Equal("sample.txt", Assert.Single(archive.Entries).FullName);
    }

    private static IEnumerable<HttpRequestMessage> SubmissionRequests(string collection, string item, string access)
    {
        var change = new { jsonData = """{"a":"changed"}""" };
        yield return new(HttpMethod.Get, access);
        yield return new(HttpMethod.Get, collection);
        yield return new(HttpMethod.Get, item);
        yield return new(HttpMethod.Patch, item) { Content = JsonContent.Create(change) };
        yield return new(HttpMethod.Put, item) { Content = JsonContent.Create(change) };
        yield return new(HttpMethod.Post, $"{item}/files") { Content = JsonContent.Create(new { }) };
        yield return new(HttpMethod.Post, $"{item}/access-token") { Content = JsonContent.Create(new { expiryMinutes = 60, permissions = new[] { "view" } }) };
        yield return new(HttpMethod.Post, $"{item}/status") { Content = JsonContent.Create(new { status = "read" }) };
        yield return new(HttpMethod.Patch, $"{item}/test") { Content = JsonContent.Create(new { isTest = true }) };
        yield return new(HttpMethod.Post, $"{collection}/test/delete") { Content = JsonContent.Create(new { }) };
        yield return new(HttpMethod.Delete, item);
    }
}
