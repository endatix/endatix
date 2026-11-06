using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Identity;

/// <summary>
/// A role change applies to the user's next request, with the access token they already hold, on every API
/// instance: permissions are cached per token, and the cache key carries the authorization stamp each role change
/// writes.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P0")]
public sealed class RoleChangeAuthorizationTests
{
    private const string SeedPassword = "Password123!";
    private const string NoCreateRole = "SubmissionsManager";
    private const string CreateRole = "FormCreator";
    private const string UserName = "role-change@example.com";

    /// <summary>Hub access, view forms, and every submission permission: no <c>forms.create</c>.</summary>
    private static readonly string[] NoCreatePermissions =
    [
        Actions.Access.Hub,
        Actions.Forms.View,
        Actions.Submissions.Create,
        Actions.Submissions.Delete,
        Actions.Submissions.Edit,
        Actions.Submissions.Export,
        Actions.Submissions.View,
    ];

    private static readonly string[] CreatePermissions = [.. NoCreatePermissions, Actions.Forms.Create];

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public RoleChangeAuthorizationTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task User_whose_only_role_lacks_forms_create_cannot_create_a_form()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareWorldAsync(NoCreateRole, ct);
        using var user = await SignInAsync(world, NoCreateRole, ct);

        (await CreateFormAsync(user, ct)).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Removing_forms_create_from_a_role_applies_to_a_token_issued_before_the_edit()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareWorldAsync(CreateRole, ct);
        using var user = await SignInAsync(world, CreateRole, ct);
        (await CreateFormAsync(user, ct)).Should().Be(HttpStatusCode.Created);

        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        await UpdateRoleAsync(admin, CreateRole, NoCreatePermissions, ct);

        (await CreateFormAsync(user, ct)).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Replacing_a_users_roles_applies_to_a_token_issued_before_the_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareWorldAsync(CreateRole, ct);
        long tenantId = world.Tenants[0].Id;
        await SeedRoleAsync(world, tenantId, NoCreateRole, NoCreatePermissions, ct);
        using var user = await SignInAsync(world, CreateRole, ct);
        (await CreateFormAsync(user, ct)).Should().Be(HttpStatusCode.Created);

        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        long userId = await UserIdAsync(world, tenantId, ct);
        using var replaced = await admin.PutAsJsonAsync(
            new Uri($"/api/users/{userId}/roles", UriKind.Relative),
            new { userId, roleNames = new[] { NoCreateRole } },
            ct);
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);

        (await CreateFormAsync(user, ct)).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_role_edit_on_one_instance_applies_on_another_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareWorldAsync(CreateRole, ct);
        using var user = await SignInAsync(world, CreateRole, ct);

        await using EndatixWebApplicationFactory otherInstance = new(
            _fixture.Database.ConnectionString, _fixture.Database.Provider);
        using var userOnOtherInstance = otherInstance.CreateClient();
        userOnOtherInstance.DefaultRequestHeaders.Authorization = user.DefaultRequestHeaders.Authorization;
        (await CreateFormAsync(userOnOtherInstance, ct)).Should().Be(HttpStatusCode.Created);

        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        await UpdateRoleAsync(admin, CreateRole, NoCreatePermissions, ct);

        (await CreateFormAsync(userOnOtherInstance, ct)).Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<IntegrationTestWorld> PrepareWorldAsync(string userRole, CancellationToken ct)
    {
        var world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        long tenantId = world.Tenants[0].Id;
        string[] permissions = userRole == CreateRole ? CreatePermissions : NoCreatePermissions;
        await SeedRoleAsync(world, tenantId, userRole, permissions, ct);

        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserAsync(tenantId, UserName, UserName, password: SeedPassword, cancellationToken: ct);
        await builder.SeedUserRoleAssignmentAsync(tenantId, UserName, userRole, ct);
        return world;
    }

    private static async Task SeedRoleAsync(
        IntegrationTestWorld world, long tenantId, string roleName, string[] permissions, CancellationToken ct)
    {
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(tenantId, roleName, ct);
        await builder.SeedRolePermissionsAsync(tenantId, roleName, permissions, ct);
    }

    private static Task<HttpClient> SignInAsync(IntegrationTestWorld world, string roleName, CancellationToken ct) =>
        world.AsAsync(TestPersona.CustomRole(roleName), userName: UserName, cancellationToken: ct);

    private static async Task<long> UserIdAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await identityDb.Users
            .Where(user => user.TenantId == tenantId && user.UserName == UserName)
            .Select(user => user.Id)
            .SingleAsync(ct);
    }

    private static async Task UpdateRoleAsync(HttpClient admin, string roleName, string[] permissions, CancellationToken ct)
    {
        using var response = await admin.PutAsJsonAsync(
            new Uri($"/api/roles/{roleName}", UriKind.Relative),
            new { roleName, permissions },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpStatusCode> CreateFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/forms", UriKind.Relative),
            new { name = $"Role change {Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = "{}" },
            ct);
        return response.StatusCode;
    }
}
