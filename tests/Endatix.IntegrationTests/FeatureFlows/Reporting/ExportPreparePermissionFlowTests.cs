using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Who may prepare a form for export over HTTP on <see cref="ReportingWebHost"/>: <c>compile-schema</c> and
/// <c>submissions/backfill</c> accept <c>submissions.export</c> or <c>forms.edit</c>, while a full rebuild
/// (<c>replace</c>, <c>force</c>), which clears flattened data, still needs <c>forms.edit</c>.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class ExportPreparePermissionFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SEED_PASSWORD = "Password123!";

    private static readonly string[] _exporterPermissions =
        [Actions.Access.Hub, Actions.Forms.View, Actions.Submissions.View, Actions.Submissions.Export];

    private static readonly string[] _editorPermissions =
        [Actions.Access.Hub, Actions.Forms.View, Actions.Forms.Edit];

    [Fact]
    public async Task A_user_who_may_export_but_not_edit_compiles_and_backfills()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var (admin, world) = await TenantAdminAsync(host, ct);
        var formId = await CreateFormWithSubmissionAsync(admin, ct);
        using var exporter = await CustomRoleUserAsync(host, world, "Exporter", _exporterPermissions, ct);

        // Act
        using var compiled = await CompileAsync(exporter, formId, replace: false, ct);
        using var backfilled = await BackfillAsync(exporter, formId, force: false, ct);

        // Assert
        compiled.StatusCode.Should().Be(HttpStatusCode.OK, await compiled.Content.ReadAsStringAsync(ct));
        backfilled.StatusCode.Should().Be(HttpStatusCode.OK, await backfilled.Content.ReadAsStringAsync(ct));
        using var body = JsonDocument.Parse(await backfilled.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("processed").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_user_who_may_export_but_not_edit_cannot_rebuild_from_scratch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var (admin, world) = await TenantAdminAsync(host, ct);
        var formId = await CreateFormWithSubmissionAsync(admin, ct);
        using var exporter = await CustomRoleUserAsync(host, world, "Exporter", _exporterPermissions, ct);

        // Act
        using var replaced = await CompileAsync(exporter, formId, replace: true, ct);
        using var forced = await BackfillAsync(exporter, formId, force: true, ct);

        // Assert
        replaced.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        forced.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_user_who_may_edit_but_not_export_rebuilds_from_scratch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var (admin, world) = await TenantAdminAsync(host, ct);
        var formId = await CreateFormWithSubmissionAsync(admin, ct);
        using var editor = await CustomRoleUserAsync(host, world, "Editor", _editorPermissions, ct);

        // Act
        using var replaced = await CompileAsync(editor, formId, replace: true, ct);
        using var forced = await BackfillAsync(editor, formId, force: true, ct);

        // Assert
        replaced.StatusCode.Should().Be(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync(ct));
        forced.StatusCode.Should().Be(HttpStatusCode.OK, await forced.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task A_user_who_may_neither_export_nor_edit_cannot_prepare()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var (admin, world) = await TenantAdminAsync(host, ct);
        var formId = await CreateFormWithSubmissionAsync(admin, ct);
        using var viewer = await CustomRoleUserAsync(
            host, world, "Viewer", [Actions.Access.Hub, Actions.Forms.View, Actions.Submissions.View], ct);

        // Act
        using var compiled = await CompileAsync(viewer, formId, replace: false, ct);
        using var backfilled = await BackfillAsync(viewer, formId, force: false, ct);

        // Assert
        compiled.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        backfilled.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<HttpResponseMessage> CompileAsync(HttpClient client, string formId, bool replace, CancellationToken ct) =>
        client.PostAsJsonAsync($"/api/forms/{formId}/reporting/compile-schema", new { replace }, ct);

    private static Task<HttpResponseMessage> BackfillAsync(HttpClient client, string formId, bool force, CancellationToken ct) =>
        client.PostAsJsonAsync($"/api/forms/{formId}/submissions/backfill", new { completionScope = "completed", force }, ct);

    private static async Task<(HttpClient Admin, IntegrationTestWorld World)> TenantAdminAsync(
        ReportingWebHost host,
        CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        return (await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct), world);
    }

    private static async Task<HttpClient> CustomRoleUserAsync(
        ReportingWebHost host,
        IntegrationTestWorld world,
        string roleName,
        string[] permissions,
        CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        var userName = $"{roleName.ToLowerInvariant()}-user";
        await host.Seed.SeedRoleAsync(tenantId, roleName, ct);
        await host.Seed.SeedRolePermissionsAsync(tenantId, roleName, permissions, ct);
        await host.Seed.SeedUserAsync(tenantId, userName, $"{userName}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await host.Seed.SeedUserRoleAssignmentAsync(tenantId, userName, roleName, ct);
        return await world.AsAsync(TestPersona.CustomRole(roleName), userName: userName, cancellationToken: ct);
    }

    private static async Task<string> CreateFormWithSubmissionAsync(HttpClient admin, CancellationToken ct)
    {
        using var created = await admin.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"export-prepare-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""",
            },
            ct);
        created.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var formId = form.RootElement.GetProperty("id").GetString()!;

        using var submitted = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions", new { isComplete = true, jsonData = """{"q1":"answer"}""" }, ct);
        submitted.EnsureSuccessStatusCode();
        return formId;
    }
}
