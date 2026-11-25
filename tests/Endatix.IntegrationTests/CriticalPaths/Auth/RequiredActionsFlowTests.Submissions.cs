using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
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
        var tokens = Substitute.For<ISubmissionAccessTokenService>();
        var files = Substitute.For<ISubmissionFileExtractor>();
        files.ExtractFilesAsync(default, 0, 0, "", ct).ReturnsForAnyArgs(_ =>
            [new("sample.txt", "text/plain", new MemoryStream([42]))]);
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddSingleton<IRequiredActionProvider>(action);
            services.AddSingleton(files);
            services.AddSingleton(tokens);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var target = await SubmissionTarget.CreateAsync(owner, ct);
        using var warm = await owner.GetAsync(target.Access, ct);
        warm.EnsureSuccessStatusCode();
        action.Pending = true;
        await InvalidateAsync(world, ct);
        commands.Count = 0;
        await AssertSubmissionRefusalsAsync(owner, target, ct);
        Assert.Equal(0, commands.Count);
        Assert.Empty(files.ReceivedCalls());
        Assert.Empty(tokens.ReceivedCalls());

        action.Pending = false;
        await InvalidateAsync(world, ct);
        using var download = await owner.PostAsJsonAsync($"{target.Item}/files", new { }, ct);
        download.EnsureSuccessStatusCode();
        using var archive = new ZipArchive(await download.Content.ReadAsStreamAsync(ct));
        Assert.Equal("sample.txt", Assert.Single(archive.Entries).FullName);
    }

    private static async Task AssertSubmissionRefusalsAsync(HttpClient owner, SubmissionTarget target, CancellationToken ct)
    {
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        foreach (var request in target.Requests())
        {
            using var sent = request;
            using var response = await owner.SendAsync(sent, ct);
            await AssertStandingRefusalAsync(response, me, ct);
        }
    }

    [Fact]
    public async Task Managed_standing_is_read_once_before_product_work_and_never_for_a_respondent_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData()));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var target = await SubmissionTarget.CreateAsync(owner, ct);
        using var issued = await owner.PostAsJsonAsync($"{target.Item}/access-token", new { permissions = new[] { "edit", "view" }, expiryMinutes = 30 }, ct);
        var token = (await issued.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("token").GetString();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        foreach (var request in target.Requests())
        {
            using var sent = request;
            snapshots.ClearReceivedCalls();
            using var response = await owner.SendAsync(sent, ct);
            if (sent.RequestUri!.AbsolutePath == target.Access)
            {
                // The resource policy already holds its real authorized snapshot.
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Empty(snapshots.ReceivedCalls());
                continue;
            }
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }
        snapshots.ClearReceivedCalls();
        using var anonymous = world.AnonymousClient();
        var byToken = $"{target.Collection}/by-access-token/{token}";
        using var read = await anonymous.GetAsync(byToken, ct);
        using var update = await anonymous.PatchAsJsonAsync(byToken, new { jsonData = """{"a":"respondent"}""" }, ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());
    }

    [Fact]
    public async Task Managed_submissions_preserve_hidden_targets_and_missing_grants_before_unavailability()
    {
        var ct = TestContext.Current.CancellationToken;
        var pending = new MutableProvider("conflict", "/elsewhere") { Pending = false };
        var otherAction = new MutableProvider("setup", "/setup") { Pending = false };
        var commands = new ContentCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddSingleton<IRequiredActionProvider>(pending);
            services.AddSingleton<IRequiredActionProvider>(otherAction);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var restricted = await RestrictedUserAsync(world, ct);
        var visible = await SubmissionTarget.CreateAsync(owner, ct);
        var hidden = await SubmissionTarget.CreateAsync(other, ct);
        pending.Pending = otherAction.Pending = true;
        await InvalidateAsync(world, ct);

        commands.Count = 0;
        await AssertContentStatusAsync(owner, hidden.Requests(), HttpStatusCode.NotFound);
        await AssertContentStatusAsync(restricted, visible.Requests(), HttpStatusCode.Forbidden);
        await AssertContentStatusAsync(owner, visible.Requests(), HttpStatusCode.ServiceUnavailable);
        Assert.Equal(0, commands.Count);
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(true, null)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Managed_submissions_allow_notice_empty_and_dormant_standing(bool enforce, RequiredActionSeverity? severity)
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
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var target = await SubmissionTarget.CreateAsync(owner, ct);
        await AssertContentStatusAsync(owner, target.Requests(), HttpStatusCode.OK);
    }

    private sealed record SubmissionTarget(string Form, string Id)
    {
        internal string FormRoute => $"/api/forms/{Form}";
        internal string Collection => $"{FormRoute}/submissions";
        internal string Item => $"{Collection}/{Id}";
        internal string Access => $"/api/access/forms/{Form}/submissions/{Id}";

        internal static async Task<SubmissionTarget> CreateAsync(HttpClient owner, CancellationToken ct) =>
            await OnFormAsync(owner, await CreateFormAsync(owner, ct), ct);

        internal Task<SubmissionTarget> SiblingAsync(HttpClient owner, CancellationToken ct) => OnFormAsync(owner, Form, ct);

        private static async Task<SubmissionTarget> OnFormAsync(HttpClient owner, string form, CancellationToken ct)
        {
            var collection = $"/api/forms/{form}/submissions";
            using var created = await owner.PostAsJsonAsync(collection, new { jsonData = """{"a":"sample"}""" }, ct);
            created.EnsureSuccessStatusCode();
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!;
            return new(form, id);
        }

        internal IEnumerable<HttpRequestMessage> LifecycleRequests()
        {
            yield return new(HttpMethod.Get, Access);
            yield return new(HttpMethod.Get, Collection);
            yield return new(HttpMethod.Post, $"{Item}/access-token") { Content = JsonContent.Create(new { expiryMinutes = 60, permissions = new[] { "view" } }) };
        }

        internal IEnumerable<HttpRequestMessage> Requests()
        {
            foreach (var request in LifecycleRequests())
            {
                yield return request;
            }
            var change = new { jsonData = """{"a":"changed"}""" };
            yield return new(HttpMethod.Get, Item);
            yield return new(HttpMethod.Patch, Item) { Content = JsonContent.Create(change) };
            yield return new(HttpMethod.Put, Item) { Content = JsonContent.Create(change) };
            yield return new(HttpMethod.Post, $"{Item}/files") { Content = JsonContent.Create(new { }) };
            yield return new(HttpMethod.Post, $"{Item}/status") { Content = JsonContent.Create(new { status = "read" }) };
            yield return new(HttpMethod.Patch, $"{Item}/test") { Content = JsonContent.Create(new { isTest = false }) };
            yield return new(HttpMethod.Post, $"{Collection}/test/delete") { Content = JsonContent.Create(new { }) };
            yield return new(HttpMethod.Delete, Item);
        }
    }
}
