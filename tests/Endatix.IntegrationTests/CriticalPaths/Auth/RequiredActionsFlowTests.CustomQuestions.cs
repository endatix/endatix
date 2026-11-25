using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
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
    public async Task Unavailable_standing_precedes_question_data_access_but_not_ordinary_checks()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        var questions = Substitute.For<IRepository<CustomQuestion>>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddScoped(_ => new AccountStanding(snapshots));
            services.AddSingleton(questions);
        });
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
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());

        using var list = await admin.GetAsync(route, ct);
        await AssertUnavailableQuestionsAsync(list, ct);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        Assert.Empty(questions.ReceivedCalls());
        snapshots.ClearReceivedCalls();
        using var create = await admin.PostAsJsonAsync(route, new { name = "Example", jsonData = """{"type":"text"}""" }, ct);
        await AssertUnavailableQuestionsAsync(create, ct);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        await questions.Received(1).AnyAsync(Arg.Any<CustomQuestionSpecifications.ByName>(), Arg.Any<CancellationToken>());
        Assert.Single(questions.ReceivedCalls());
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

        provider.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        using var duplicate = await admin.PostAsJsonAsync(route, original, ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync(ct));
        using var blockedRead = await admin.GetAsync(route, ct);
        await AssertStandingRefusalAsync(blockedRead, me, ct);
        using var blockedWrite = await admin.PostAsJsonAsync(route, another, ct);
        await AssertStandingRefusalAsync(blockedWrite, me, ct);

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(JsonElement.DeepEquals(before, after));
        using var resumed = await admin.PostAsJsonAsync(route, another, ct);
        Assert.Equal(HttpStatusCode.Created, resumed.StatusCode);
    }
}
