using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Test as this person and Start over: each call opens a new test submission on the current definition with the
/// person's current values, and leaves the person's personalised link and Submitter as they were.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_test_submission_twice_opens_two_and_the_second_has_the_changed_value()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var membershipId = MembershipIdOf(person);

        // Act
        var first = await OpenTestSubmissionAsync(admin, person.FormId, membershipId);
        using var changed = await admin.PutValueAsync(person.PersonUrl, person.PropertyId, "Dayton");
        var second = await OpenTestSubmissionAsync(admin, person.FormId, membershipId);

        // Assert
        Assert.NotEqual(IdOf(first, "submissionId"), IdOf(second, "submissionId"));
        Assert.False(string.IsNullOrEmpty(first.GetProperty("token").GetString()));
        var firstRead = await SubmissionAsync(admin, person.FormId, IdOf(first, "submissionId"));
        var secondRead = await SubmissionAsync(admin, person.FormId, IdOf(second, "submissionId"));
        Assert.True(firstRead.GetProperty("isTestSubmission").GetBoolean());
        Assert.Equal("test", firstRead.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", CityOf(firstRead));
        Assert.Equal("Dayton", CityOf(secondRead));
    }

    [Fact]
    public async Task Opening_test_submissions_leaves_the_persons_link_and_submitters_unchanged()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        await RedeemAsync(anonymous, person.FormId, person.Token);
        var before = await LinkStateAsync(world.Services, person.FormId);

        // Act
        await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));

        // Assert
        Assert.Equal(before, await LinkStateAsync(world.Services, person.FormId));
    }

    [Fact]
    public async Task Opening_a_test_submission_without_forms_test_is_forbidden()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var creator = await AsAsync(world, TestPersona.Creator);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");

        // Act
        using var response = await creator.PostAsync(
            TestSubmissionsUrl(person.FormId), new { membershipId = MembershipIdOf(person) });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test_submissions_are_left_out_of_a_default_export()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, realEmail) = await FormWithPersonAsync(api, "Columbus");
        var testEmail = $"{Guid.NewGuid():N}@example.com";
        using var added = await api.AddPersonAsync(formId, testEmail);
        await CompletedOnBehalfAsync(api, formId, realEmail);
        await OpenTestSubmissionAsync(api, formId, IdOf(await api.ReadAsync(added), "membershipId"));
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        await ExpectOkAsync(api.PostAsync(
            $"/api/forms/{formId}/submissions/backfill", new { completionScope = "incomplete" }));

        // Act
        var byDefault = await CsvExportAsync(api, formId, includeTestSubmissions: null);
        var withTests = await CsvExportAsync(api, formId, includeTestSubmissions: true);

        // Assert
        Assert.Contains(realEmail, byDefault);
        Assert.DoesNotContain(testEmail, byDefault);
        Assert.Contains(testEmail, withTests);
    }

    [Fact]
    public async Task Starting_over_deletes_the_test_submission_and_opens_a_new_one_with_current_values()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        using var changed = await admin.PutValueAsync(person.PersonUrl, person.PropertyId, "Dayton");

        // Act
        using var restarted = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) });

        // Assert
        Assert.Equal(HttpStatusCode.Created, restarted.StatusCode);
        var next = await admin.ReadAsync(restarted);
        Assert.NotEqual(openedId, IdOf(next, "submissionId"));
        Assert.False(string.IsNullOrEmpty(next.GetProperty("token").GetString()));
        Assert.Equal("Dayton", CityOf(await SubmissionAsync(admin, person.FormId, IdOf(next, "submissionId"))));
        using var old = await admin.Client.GetAsync(
            $"/api/forms/{person.FormId}/submissions/{openedId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    [Fact]
    public async Task Starting_over_a_submission_not_opened_as_a_test_is_rejected()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();
        var redeemed = await RedeemAsync(anonymous, person.FormId, person.Token);
        var linkSubmissionId = IdOf(redeemed, "submissionId");
        var linkSubmissionToken = await SubmissionTokenAsync(world.Services, linkSubmissionId);

        // Act
        using var response = await admin.PostAsync(
            RestartUrl(person.FormId, linkSubmissionId), new { token = linkSubmissionToken });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Starting_over_after_the_person_is_removed_is_not_found_and_keeps_the_submission()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        using var removed = await admin.Client.DeleteAsync(person.PersonUrl, admin.CancellationToken);

        // Act
        using var response = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var kept = await admin.Client.GetAsync(
            $"/api/forms/{person.FormId}/submissions/{openedId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    [Fact]
    public async Task Starting_over_without_the_submissions_token_is_not_found_and_keeps_the_submission()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var openedId = IdOf(await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person)), "submissionId");
        var another = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));

        // Act
        using var response = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(another) });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var kept = await admin.Client.GetAsync(
            $"/api/forms/{person.FormId}/submissions/{openedId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    [Fact]
    public async Task Starting_over_with_no_token_is_rejected()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var openedId = IdOf(await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person)), "submissionId");

        // Act
        using var response = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Two Start overs of one test submission, forced to overlap: the first has deleted the old submission and
    /// inserted its replacement, and is paused before it commits, when the second arrives. The second waits on the
    /// old submission's row, then finds it deleted, so the person is left with one new test submission, not two.
    /// </summary>
    [Fact]
    public async Task Starting_over_twice_at_once_opens_one_replacement()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        var command = new RestartTestSubmissionCommand(
            await TenantIdOfAsync(world.Services, person.FormId), person.FormId, openedId, TokenOf(opened));
        var gate = new CommitGate();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gatedApp = GatedAppDbContext(scope.ServiceProvider, gate);
        var first = RestartHandlerWith(scope.ServiceProvider, gatedApp).Handle(command, admin.CancellationToken);
        await Task.WhenAny(gate.Reached, first).WaitAsync(_gateTimeout, admin.CancellationToken);
        Assert.True(gate.Reached.IsCompleted, "The first Start over never reached its commit.");

        // Act
        var second = admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) });
        var secondWaited = await Task.WhenAny(second, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != second;
        gate.Release();
        var firstResult = await first.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var secondResponse = await second.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(secondWaited, "The second Start over did not wait for the first one's delete.");
        Assert.Equal(ResultStatus.Created, firstResult.Status);
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
        Assert.Equal([firstResult.Value.SubmissionId], await LiveSubmissionIdsAsync(world.Services, person.FormId));
    }

    /// <summary>
    /// The first Start over has read the submission and is paused before it takes the form's lock when a second one
    /// replaces it. The first's delete then matches no live row, so it saves nothing and is not found.
    /// </summary>
    [Fact]
    public async Task Starting_over_a_test_submission_another_start_over_replaced_meanwhile_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var restartUrl = RestartUrl(person.FormId, IdOf(opened, "submissionId"));
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormLockStatement());
        var first = requests.Add(admin.PostAsync(restartUrl, new { token = TokenOf(opened) }));
        await PausedAsync(pause, first);

        // Act
        using var second = await admin.PostAsync(restartUrl, new { token = TokenOf(opened) })
            .WaitAsync(_gateTimeout, admin.CancellationToken);
        pause.Release();
        using var firstResponse = await first.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, firstResponse.StatusCode);
        Assert.Equal(
            [IdOf(await admin.ReadAsync(second), "submissionId")],
            await LiveSubmissionIdsAsync(world.Services, person.FormId));
    }

    [Fact]
    public async Task Starting_over_a_test_submission_of_another_tenant_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var otherTenantId = await TenantIdOfAsync(world.Services, person.FormId) + 1;
        await using var scope = world.Services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var result = await RestartHandlerWith(scope.ServiceProvider, app).Handle(
            new RestartTestSubmissionCommand(otherTenantId, person.FormId, IdOf(opened, "submissionId"), TokenOf(opened)),
            admin.CancellationToken);

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    private static string TestSubmissionsUrl(long formId) => $"/api/forms/{formId}/audience/test-submissions";

    private static string RestartUrl(long formId, long submissionId) =>
        $"{TestSubmissionsUrl(formId)}/{submissionId}/restart";

    private static long MembershipIdOf(IssuedLink person) => long.Parse(person.PersonUrl.Split('/')[^1]);

    private static string TokenOf(JsonElement opened) => opened.GetProperty("token").GetString()!;

    /// <summary>The host's Start over handler, with every <see cref="AppDbContext"/> read and write going through <paramref name="app"/>.</summary>
    private static RestartTestSubmissionHandler RestartHandlerWith(IServiceProvider services, AppDbContext app)
    {
        var evaluator = services.GetRequiredService<EndatixSpecificationEvaluator>();
        var audience = services.GetRequiredService<IAudienceDbContext>();
        var forms = new EfRepository<Form>(app, evaluator);
        var submissions = new EfRepository<Submission>(app, evaluator);
        var tokens = new SubmissionTokenService(submissions, forms, new EfRepository<TenantSettings>(app, evaluator));
        var gates = new LinkOpenGates(
            audience,
            forms,
            new EfRepository<Submitter>(app, evaluator),
            services.GetRequiredService<ISubmitterResolver>());
        var formLock = new FormSubmissionsLock(app, services.GetRequiredService<ITransactionLock>());
        return new RestartTestSubmissionHandler(
            submissions,
            new TestSubjects(audience, forms, gates),
            new TestSubmissionOpener(audience, tokens, new TestSubmissionWriter(submissions, forms, formLock)));
    }

    private static async Task<long> TenantIdOfAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Forms
            .IgnoreQueryFilters()
            .Where(form => form.Id == formId)
            .Select(form => form.TenantId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> SubmissionTokenAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Submissions
            .IgnoreQueryFilters()
            .Where(submission => submission.Id == submissionId)
            .Select(submission => submission.Token!.Value)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long[]> LiveSubmissionIdsAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Submissions
            .IgnoreQueryFilters()
            .Where(submission => submission.FormId == formId && !submission.IsDeleted)
            .Select(submission => submission.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static string? CityOf(JsonElement submission) =>
        submission.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString();

    private static async Task<JsonElement> OpenTestSubmissionAsync(AudienceApi admin, long formId, long membershipId)
    {
        using var response = await admin.PostAsync(TestSubmissionsUrl(formId), new { membershipId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadAsync(response);
    }

    private static Task<JsonElement> SubmissionAsync(AudienceApi admin, long formId, long submissionId) =>
        admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);

    private static async Task<string> CsvExportAsync(AudienceApi api, long formId, bool? includeTestSubmissions)
    {
        var formats = await api.Client.GetFromJsonAsync<JsonElement>("/api/settings/export-formats", api.CancellationToken);
        var exportFormatId = IdOf(formats.EnumerateArray().First(format => format.GetProperty("wireKey").GetString() == "csv"), "id");
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportFormatId, includeTestSubmissions });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(api.CancellationToken);
    }

    /// <summary>The form's links as stored, and the tenant's Submitters with the members bound to them.</summary>
    private static async Task<string> LinkStateAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var links = await audience.Links.AsNoTracking()
            .Where(link => link.FormId == formId)
            .OrderBy(link => link.Id)
            .Select(link => new { link.Id, link.SubmissionId, link.OpenedAt, link.TokenHash })
            .ToListAsync(cancellationToken);
        var tenantId = await app.Forms.Where(form => form.Id == formId).Select(form => form.TenantId)
            .SingleAsync(cancellationToken);
        var submitters = await app.Submitters.AsNoTracking()
            .Where(submitter => submitter.TenantId == tenantId)
            .OrderBy(submitter => submitter.Id)
            .Select(submitter => submitter.Id)
            .ToListAsync(cancellationToken);
        var bound = await audience.Members.AsNoTracking()
            .Where(member => member.TenantId == tenantId)
            .OrderBy(member => member.Id)
            .Select(member => new { member.Id, member.SubmitterId })
            .ToListAsync(cancellationToken);
        Assert.NotEmpty(links);
        Assert.NotEmpty(submitters);
        return JsonSerializer.Serialize(new { links, submitters, bound });
    }

    /// <summary>
    /// Pauses the first commit of the context it is added to, until released: the transaction's statements have run
    /// and it still holds their locks.
    /// </summary>
    private sealed class CommitGate : DbTransactionInterceptor
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => _reached.Task;

        public void Release() => _released.TrySetResult();

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (_reached.TrySetResult())
            {
                await _released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
