using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task A_busy_question_catalog_returns_a_retryable_failure_without_creating_a_question()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var locks = scope.ServiceProvider.GetRequiredService<ITransactionLock>();
        var key = new TransactionLockRequest(TransactionLockScopes.CustomQuestionNames, world.Tenants[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await using var held = await locks.BeginLockedTransactionAsync(db.Database, key, ct);

        // Act
        using var response = await admin.PostAsJsonAsync("/api/questions", new { name = "Rating", jsonData = """{"type":"rating"}""" }, ct);

        // Assert
        await AssertUnavailableQuestionsAsync(response, ct);
        var page = await admin.GetFromJsonAsync<JsonElement>("/api/questions", ct);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_creates_of_one_question_name_leave_one_question()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await using var host = new ReportingWebHost(fixture, services => services.ConfigureDbContext<AppDbContext>(options =>
        {
            if (fixture.Database.Provider == TestDatabaseProvider.SqlServer)
            {
                options.UseSqlServer(fixture.Database.ConnectionString, sql => sql.EnableRetryOnFailure());
            }
            else
            {
                options.UseNpgsql(fixture.Database.ConnectionString, pg => pg.EnableRetryOnFailure());
            }
        }));
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        const string route = "/api/questions";
        var question = new { name = "Rating", jsonData = """{"type":"rating"}""" };

        // Act
        var first = admin.PostAsJsonAsync(route, question, ct);
        var second = admin.PostAsJsonAsync(route, question with { name = "RATING" }, ct);
        await Task.WhenAll(first, second);
        using var left = await first;
        using var right = await second;

        // Assert
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.BadRequest], new[] { left.StatusCode, right.StatusCode }.Order());
        var page = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Ordinary_question_creation_still_requires_permission_and_valid_input()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var anonymous = world.AnonymousClient();
        using var reader = await RestrictedUserAsync(world, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        const string route = "/api/questions";

        // Act
        using var challenge = await anonymous.PostAsJsonAsync(route, new { }, ct);
        using var denied = await reader.PostAsJsonAsync(route, new { }, ct);
        using var invalid = await admin.PostAsJsonAsync(route, new { }, ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Two_tenants_can_each_create_a_custom_question_with_the_same_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        var question = new { name = "Service rating", jsonData = """{"type":"rating"}""" };
        foreach (var tenantIndex in new[] { 0, 1 })
        {
            using var admin = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
            using var created = await admin.PostAsJsonAsync("/api/questions", question, ct);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var page = await admin.GetFromJsonAsync<JsonElement>("/api/questions", ct);
            var item = Assert.Single(page.GetProperty("items").EnumerateArray());
            var saved = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(saved.GetProperty("id").GetString(), item.GetProperty("id").GetString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Custom_questions_allow_notices_and_no_provider(bool hasNotice)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (hasNotice)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("help", "/help") { Severity = RequiredActionSeverity.Notice });
            }
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var question = new { name = "Travel rating", jsonData = """{"type":"rating"}""" };
        using var created = await admin.PostAsJsonAsync("/api/questions", question, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var page = await admin.GetFromJsonAsync<JsonElement>("/api/questions", ct);
        Assert.Equal(question.name, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unavailable_standing_precedes_question_validation_and_data_access_after_route_authorization()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var anonymous = host.CreateClient();
        using var reader = await RestrictedUserAsync(world, ct);
        const string route = "/api/questions";
        using var challenge = await anonymous.GetAsync(route, ct);
        using var denied = await reader.GetAsync(route, ct);
        using var invalid = await admin.PostAsJsonAsync(route, new { }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await AssertUnavailableQuestionsAsync(invalid, ct);
        using var invalidPage = await admin.GetAsync($"{route}?pageSize=0", ct);
        await AssertUnavailableQuestionsAsync(invalidPage, ct);
        using var create = await admin.PostAsJsonAsync(route, new { name = "Example", jsonData = """{"type":"text"}""" }, ct);
        await AssertUnavailableQuestionsAsync(create, ct);
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(AdmittedSnapshot()));
        var page = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    private static async Task AssertUnavailableQuestionsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("items", out _));
        Assert.False(problem.TryGetProperty("jsonData", out _));
    }

    [Fact]
    public async Task Custom_questions_preserve_duplicate_validation_and_resume_after_standing_clears()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("terms", "/terms") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        const string route = "/api/questions";
        var original = new { name = "Sample rating", jsonData = """{"type":"rating"}""" };
        var another = original with { name = "Second rating" };
        using var created = await admin.PostAsJsonAsync(route, original, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var before = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.Equal(original.name, Assert.Single(before.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());

        using var duplicate = await admin.PostAsJsonAsync(route, original, ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync(ct));
        provider.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        using var blockedRead = await admin.GetAsync(route, ct);
        await AssertStandingRefusalAsync(blockedRead, me, ct);
        using var blockedWrite = await admin.PostAsJsonAsync(route, another, ct);
        await AssertStandingRefusalAsync(blockedWrite, me, ct);
        using var blockedDuplicate = await admin.PostAsJsonAsync(route, original, ct);
        await AssertStandingRefusalAsync(blockedDuplicate, me, ct);

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(JsonElement.DeepEquals(before, after));
        using var resumed = await admin.PostAsJsonAsync(route, another, ct);
        Assert.Equal(HttpStatusCode.Created, resumed.StatusCode);
    }
}
