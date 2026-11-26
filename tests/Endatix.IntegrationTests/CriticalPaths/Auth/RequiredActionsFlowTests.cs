using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Features.FormSchema;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed partial class RequiredActionsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task Form_access_returns_standing_before_the_resource_denial_for_a_pending_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var signedIn = await RestrictedUserAsync(world, ct);
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("v1", "/profile"));
            EnableStanding(services);
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Equal("blocking", Assert.Single(me.GetProperty("requiredActions").EnumerateArray()).GetProperty("severity").GetString());

        using var response = await client.GetAsync("/api/access/forms/1", ct);

        await AssertStandingRefusalAsync(response, me, ct);
    }

    [Fact]
    public async Task Authorized_form_access_returns_the_same_actions_as_me_instead_of_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("v1", "/profile"));
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("v2", "/profile"));
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("notice", "/help")
            { Key = "help", Severity = RequiredActionSeverity.Notice });
            EnableStanding(services);
        }));
        using var signedIn = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        using var response = await client.GetAsync("/api/access/forms/1", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
        Assert.Equal(["help", "profile.complete"], problem.GetProperty("requiredActions").EnumerateArray().Select(a => a.GetProperty("key").GetString()));
        Assert.True(JsonElement.DeepEquals(me.GetProperty("requiredActions"), problem.GetProperty("requiredActions")));
        Assert.False(problem.TryGetProperty("permissions", out _));
    }

    private static void EnableStanding(IServiceCollection services) =>
        services.AddRequiredActionEnforcement();

    private static AuthorizationData AdmittedSnapshot() => AuthorizationData.ForAuthenticatedUser("owner", 1, [], []);

    private static async Task<HttpClient> RestrictedUserAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        var tenant = world.Tenants[0].Id;
        const string role = "ExportOnly";
        const string user = "exporter@example.test";
        await world.Seed.SeedRoleAsync(tenant, role, ct);
        await world.Seed.SeedRolePermissionsAsync(tenant, role, [Actions.Access.Hub, Actions.Submissions.Export], ct);
        await world.Seed.SeedUserAsync(tenant, user, user, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenant, user, role, ct);
        return await world.AsAsync(TestPersona.CustomRole(role), mode: IntegrationAuthMode.SyntheticJwt, userName: user, cancellationToken: ct);
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Notice_only_and_the_unactivated_host_allow_ordinary_reads_and_writes(bool proofHost, RequiredActionSeverity severity)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("v1", "/profile") { Severity = severity });
                if (proofHost)
                {
                    EnableStanding(services);
                }
            });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var signedIn = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(signedIn, ct);
        using var client = host.CreateClient();
        using var challenge = await client.GetAsync("/api/auth/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var action = Assert.Single(me.GetProperty("requiredActions").EnumerateArray());
        Assert.Equal(severity == RequiredActionSeverity.Blocking ? "blocking" : "notice", action.GetProperty("severity").GetString());
        var first = await client.GetFromJsonAsync<JsonElement>($"/api/access/forms/{form}", ct);
        var cached = await client.GetFromJsonAsync<JsonElement>($"/api/access/forms/{form}", ct);
        using var compiled = await client.PostAsJsonAsync($"/api/forms/{form}/reporting/compile-schema", new { replace = true }, ct);

        Assert.Equal(first.GetProperty("cachedAt").GetString(), cached.GetProperty("cachedAt").GetString());
        Assert.Equal(HttpStatusCode.OK, compiled.StatusCode);
        var result = await compiled.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(form, result.GetProperty("formId").ToString());
    }

    [Fact]
    public async Task Pending_editor_gets_no_schema_compile_for_either_visible_or_hidden_forms()
    {
        var ct = TestContext.Current.CancellationToken;
        var counter = new CompileCounter();
        var provider = new MutableProvider("v1", "/profile") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
            services.AddSingleton<IPipelineBehavior<CompileFormSchemaCommand, Result<CompileFormSchemaResult>>>(counter);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var editor = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var ownForm = await CreateFormAsync(editor, ct);
        var hiddenForm = await CreateFormAsync(other, ct);
        provider.Pending = true;
        await InvalidateAsync(world, ct);
        using var exporter = await RestrictedUserAsync(world, ct);
        await AssertChallengesAsync(host, ownForm, ct);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = editor.DefaultRequestHeaders.Authorization;

        using var hidden = await client.PostAsJsonAsync($"/api/forms/{hiddenForm}/reporting/compile-schema", new { replace = true }, ct);
        using var pending = await client.PostAsJsonAsync($"/api/forms/{ownForm}/reporting/compile-schema", new { replace = true }, ct);
        using var editDenied = await exporter.PostAsJsonAsync($"/api/forms/{ownForm}/reporting/compile-schema", new { replace = true }, ct);
        using var exportBlocked = await exporter.PostAsJsonAsync($"/api/forms/{ownForm}/reporting/compile-schema", new { }, ct);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        await AssertStandingRefusalAsync(hidden, me, ct);
        Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        var problem = await pending.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
        await AssertStandingRefusalAsync(editDenied, me, ct);
        Assert.Equal(HttpStatusCode.Forbidden, exportBlocked.StatusCode);
        var refused = await exportBlocked.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", refused.GetProperty("errorCode").GetString());
        Assert.Equal(0, counter.Calls);
    }

    private static async Task AssertChallengesAsync(ReportingWebHost host, string form, CancellationToken ct)
    {
        using var anonymous = host.CreateClient();
        using var read = await anonymous.GetAsync($"/api/access/forms/{form}", ct);
        using var write = await anonymous.PostAsJsonAsync($"/api/forms/{form}/reporting/compile-schema", new { }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Contains(read.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        Assert.Contains(write.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    private static async Task<string> CreateFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("/api/forms",
            new { name = "Compile target", isEnabled = true, formDefinitionJsonData = """{"pages":[]}""" }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Warm_resource_access_obeys_current_standing_and_compile_resumes_after_recovery()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = new MutableProvider("first", "/one") { Pending = false };
        var second = new MutableProvider("second", "/two") { Pending = false };
        var counter = new CompileCounter();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(first);
            services.AddSingleton<IRequiredActionProvider>(second);
            EnableStanding(services);
            services.AddSingleton<IPipelineBehavior<CompileFormSchemaCommand, Result<CompileFormSchemaResult>>>(counter);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(client, ct);
        var read = $"/api/access/forms/{form}";
        var compile = $"/api/forms/{form}/reporting/compile-schema";
        var warm = await client.GetFromJsonAsync<JsonElement>(read, ct);
        first.Pending = second.Pending = true;
        await InvalidateAsync(world, ct);

        using var unknown = await client.PostAsJsonAsync(compile, new { replace = true }, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        Assert.Equal(0, counter.Calls);
        second.Pending = false;
        using var blocked = await client.GetAsync(read, ct);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        first.Pending = false;
        await InvalidateAsync(world, ct);

        var recovered = await client.GetFromJsonAsync<JsonElement>(read, ct);
        Assert.Equal(warm.GetProperty("eTag").GetString(), recovered.GetProperty("eTag").GetString());
        using var compiled = await client.PostAsJsonAsync(compile, new { replace = true }, ct);
        Assert.Equal(HttpStatusCode.OK, compiled.StatusCode);
        Assert.Equal(1, counter.Calls);
    }

    private static async Task InvalidateAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var user = world.Tenants[0].Admin;
        await scope.ServiceProvider.GetRequiredService<ICurrentUserAuthorizationService>()
            .InvalidateAuthorizationDataCacheAsync(user.Id.ToString(), user.TenantId, ct);
    }

    [Theory]
    [InlineData("form")]
    [InlineData("submission")]
    [InlineData("template")]
    public async Task Warm_permissions_expire_at_the_earlier_cache_or_standing_deadline(string resource)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("notice", "/profile") { Severity = RequiredActionSeverity.Notice };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = resource switch
        {
            "form" => "/api/access/forms/1",
            "template" => "/api/access/form-templates/1",
            _ => (await SubmissionTarget.CreateAsync(client, ct)).Access
        };
        var warm = await client.GetFromJsonAsync<JsonElement>(route, ct);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Assert.True(warm.GetProperty("expiresAt").GetDateTime() > deadline);

        // Act
        provider.BlockingFrom = deadline;
        await InvalidateAsync(world, ct);
        var shortened = await client.GetFromJsonAsync<JsonElement>(route, ct);
        provider.BlockingFrom = null;
        await InvalidateAsync(world, ct);
        var restored = await client.GetFromJsonAsync<JsonElement>(route, ct);

        // Assert
        Assert.Equal(warm.GetProperty("eTag").GetString(), shortened.GetProperty("eTag").GetString());
        Assert.Equal(deadline, shortened.GetProperty("expiresAt").GetDateTime());
        Assert.Equal(warm.GetProperty("eTag").GetString(), restored.GetProperty("eTag").GetString());
        Assert.Equal(warm.GetProperty("expiresAt").GetDateTime(), restored.GetProperty("expiresAt").GetDateTime());
    }

    [Fact]
    public async Task Registered_account_operations_mark_only_the_resolvers_as_exempt()
    {
        // Arrange
        await using var host = new ReportingWebHost(fixture);
        var routes = host.Services.GetServices<EndpointDataSource>().SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

        // Act / Assert
        Assert.True(IsMarked("GET", "/api/auth/me"));
        Assert.True(IsMarked("POST", "/api/auth/logout"));
        Assert.True(IsMarked("POST", "/api/my-account/change-password"));
        Assert.False(IsMarked("GET", "/api/access/forms/{formId}"));
        Assert.False(IsMarked("POST", "/api/forms/{formId}/reporting/compile-schema"));

        bool IsMarked(string method, string pattern)
        {
            var endpoint = Assert.Single(routes, e => e.RoutePattern.RawText == pattern &&
                e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true);
            var definition = Assert.IsType<FastEndpoints.EndpointDefinition>(endpoint.Metadata.GetMetadata<FastEndpoints.EndpointDefinition>());
            return definition.EndpointAttributes?.OfType<AllowWithRequiredActionsAttribute>().Any() == true;
        }
    }

    [Fact]
    public async Task Resolver_operations_require_sign_in_and_allow_a_pending_user_to_change_password_and_leave()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("v1", "/profile"));
            EnableStanding(services);
        });
        const string password = "Password123!";
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = password }, ct);
        using var anonymous = host.CreateClient();
        using var member = await world.AsAsync(TestPersona.Creator, cancellationToken: ct);
        const string replacement = "UpdatedPassword123!";
        var change = new { currentPassword = password, newPassword = replacement, confirmPassword = replacement };

        using var anonymousMe = await anonymous.GetAsync("/api/auth/me", ct);
        using var anonymousChange = await anonymous.PostAsJsonAsync("/api/my-account/change-password", change, ct);
        using var anonymousLogout = await anonymous.PostAsync("/api/auth/logout", null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousChange.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLogout.StatusCode);
        var me = await member.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Equal("blocking", Assert.Single(me.GetProperty("requiredActions").EnumerateArray()).GetProperty("severity").GetString());
        using var changed = await member.PostAsJsonAsync("/api/my-account/change-password", change, ct);
        using var logout = await member.PostAsync("/api/auth/logout", null, ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    }

    private sealed class CompileCounter : IPipelineBehavior<CompileFormSchemaCommand, Result<CompileFormSchemaResult>>
    {
        public int Calls { get; private set; }

        public Task<Result<CompileFormSchemaResult>> Handle(CompileFormSchemaCommand request,
            RequestHandlerDelegate<Result<CompileFormSchemaResult>> next, CancellationToken cancellationToken)
        {
            Calls++;
            return next();
        }
    }

    [Fact]
    public async Task Me_lists_the_pending_action_for_the_signed_in_subject()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var tenant = world.Tenants[0];
        var provider = new ProfileProvider(tenant.Admin.Id.ToString(), tenant.Id);
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRequiredActionProvider>(provider)));
        using var signedIn = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        // Act
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        // Assert
        var action = Assert.Single(me.GetProperty("requiredActions").EnumerateArray());
        Assert.Equal("profile.complete", action.GetProperty("key").GetString());
        Assert.Equal("/profile", action.GetProperty("resolvePath").GetString());
        Assert.Equal("user", action.GetProperty("scope").GetString());
        Assert.Equal("blocking", action.GetProperty("severity").GetString());
        Assert.True(action.GetProperty("canResolve").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Me_returns_retryable_unavailability_until_the_conflict_clears(bool reversed)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var first = new MutableProvider("v1", "/first");
        var second = new MutableProvider("v2", "/second");
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IRequiredActionProvider>(reversed ? second : first);
            EnableStanding(services);
            services.AddSingleton<IRequiredActionProvider>(reversed ? first : second);
        }));
        using var client = host.CreateClient();
        using var signedIn = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        using var conflict = await client.GetAsync("/api/auth/me", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, conflict.StatusCode);
        Assert.Null(conflict.Headers.Location);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Service unavailable", problem.GetProperty("title").GetString());
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("requiredActions", problem.ToString());
        Assert.DoesNotContain("/first", problem.ToString());
        Assert.DoesNotContain("/second", problem.ToString());

        using var product = await client.GetAsync("/api/access/forms/1", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, product.StatusCode);
        var unavailable = await product.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", unavailable.GetProperty("detail").GetString());
        Assert.False(unavailable.TryGetProperty("errorCode", out _));
        Assert.False(unavailable.TryGetProperty("requiredActions", out _));

        second.Pending = false;
        var recovered = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.True(recovered.GetProperty("isAdmin").GetBoolean());
        Assert.Equal("/first", Assert.Single(recovered.GetProperty("requiredActions").EnumerateArray()).GetProperty("resolvePath").GetString());
    }

    private sealed class MutableProvider(string revision, string path) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => revision;
        public bool Pending { get; set; } = true;
        public string Key { get; init; } = "profile.complete";
        public RequiredActionSeverity Severity { get; init; } = RequiredActionSeverity.Blocking;
        public DateTimeOffset? BlockingFrom { get; set; }

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RequiredAction>>(Pending ? [new()
            {
                Key = Key, ResolvePath = path,
                Scope = RequiredActionScope.User, Severity = Severity, BlockingFrom = BlockingFrom
            }] : []);
    }

    private sealed class ProfileProvider(string userId, long tenantId) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => "v1";

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            Assert.Equal(userId, subject.UserId);
            Assert.Equal(tenantId, subject.TenantId);
            Assert.Contains(SystemRole.Admin.Name, subject.Roles);
            return Task.FromResult<IReadOnlyList<RequiredAction>>([new()
            {
                Key = "profile.complete", ResolvePath = "/profile",
                Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking
            }]);
        }
    }
}
