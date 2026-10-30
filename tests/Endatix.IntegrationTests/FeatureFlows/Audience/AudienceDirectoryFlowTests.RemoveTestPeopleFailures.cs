using System.Net;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Removing test participants when it can't finish: a busy form, a failed sweep, a caller who may not delete test
/// submissions. Shares the helpers of <c>AudienceDirectoryFlowTests.RemoveTestPeople.cs</c>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Removing_test_participants_again_after_the_form_was_busy_removes_them()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var testerTest = await TestSubmissionIdAsync(admin, formId, tester);
        await using (var scope = world.Services.CreateAsyncScope())
        {
            await using var import = await HoldFormLikeAnImportAsync(scope, formId, admin.CancellationToken);
            using var busy = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
        }

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(new RemovedTestPeople(1, 1, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Empty(Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
        Assert.Equal(HttpStatusCode.NotFound, await SubmissionStatusAsync(admin, formId, testerTest));
    }

    [Theory]
    [InlineData(TESTER_WITHOUT_DELETE_ROLE)]
    [InlineData(DELETER_WITHOUT_TEST_ROLE)]
    public async Task Deleting_test_submissions_without_forms_test_and_submissions_delete_is_forbidden(string role)
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword, AfterSeed = SeedRemoverRolesAsync },
            TestContext.Current.CancellationToken);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var editor = new AudienceApi(
            await world.AsAsync(TestPersona.CustomRole(role), userName: UserOf(role), cancellationToken: admin.CancellationToken),
            admin.CancellationToken);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var testerTest = await TestSubmissionIdAsync(admin, formId, tester);

        // Act
        using var response = await RemoveTestPeopleAsync(editor, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(["tester@example.com"], Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, testerTest));
    }

    [Fact]
    public async Task Removing_test_participants_again_after_the_sweep_failed_deletes_their_test_submissions()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        long[] testerTests =
        [
            await TestSubmissionIdAsync(admin, formId, tester),
            await TestSubmissionIdAsync(admin, formId, tester),
        ];
        await RemoveWithFailingSweepAsync(world, formId, tester);
        Assert.Empty(Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK], await SubmissionStatusesAsync(admin, formId, testerTests));

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemovedTestPeople(0, 2, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(
            [HttpStatusCode.NotFound, HttpStatusCode.NotFound],
            await SubmissionStatusesAsync(admin, formId, testerTests));
    }

    private const string TESTER_WITHOUT_DELETE_ROLE = "TesterWithoutDelete";
    private const string DELETER_WITHOUT_TEST_ROLE = "DeleterWithoutTest";

    private static string UserOf(string role) => $"{role.ToLowerInvariant()}@example.com";

    /// <summary>Two editors of the form's people: one may test but not delete submissions, the other the reverse.</summary>
    private static async Task SeedRemoverRolesAsync(
        IServiceProvider services,
        StandardSeedResult seed,
        CancellationToken cancellationToken)
    {
        var tenantId = seed.TenantIds[0];
        IntegrationSeedBuilder builder = new(services);
        string[] editor = [Actions.Access.Hub, Actions.Forms.View, Actions.Forms.Edit];
        foreach ((var role, var extra) in new[]
                 {
                     (TESTER_WITHOUT_DELETE_ROLE, Actions.Forms.Test),
                     (DELETER_WITHOUT_TEST_ROLE, Actions.Submissions.Delete),
                 })
        {
            await builder.SeedRoleAsync(tenantId, role, cancellationToken);
            await builder.SeedRolePermissionsAsync(tenantId, role, [.. editor, extra], cancellationToken);
            await builder.SeedUserAsync(
                tenantId, UserOf(role), UserOf(role), password: SeedPassword, cancellationToken: cancellationToken);
            await builder.SeedUserRoleAssignmentAsync(tenantId, UserOf(role), role, cancellationToken);
        }
    }

    /// <summary>
    /// The host's handler with its sweep on an application context whose first save throws: the participant is
    /// removed, and the request fails before any test submission is deleted.
    /// </summary>
    private static async Task RemoveWithFailingSweepAsync(IntegrationTestWorld world, long formId, Participant tester)
    {
        await using var scope = world.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var app = GatedAppDbContext(services, new FirstSaveFails());
        RemoveTestPeopleHandler handler = new(
            services.GetRequiredService<IRepository<Form>>(),
            services.GetRequiredService<TestParticipantRemoval>(),
            new TestSubmissionSweep(
                new EfRepository<Submission>(app, services.GetRequiredService<EndatixSpecificationEvaluator>()),
                new AppUnitOfWork(app)),
            new GrantingAuthorization());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RemoveTestPeopleCommand(world.Tenants[0].Id, formId, [tester.MembershipId], true, false),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Throws on the first save of the context it is added to.</summary>
    private sealed class FirstSaveFails : SaveChangesInterceptor
    {
        private int _saves;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _saves) == 1
                ? throw new InvalidOperationException("The sweep's save failed.")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>A caller who holds every permission: the handler is called without a signed-in user.</summary>
    private sealed class GrantingAuthorization : ICurrentUserAuthorizationService
    {
        public Task<Result> ValidateAccessAsync(string requiredPermission, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<AuthorizationData>> GetAuthorizationDataAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<bool>> HasPermissionAsync(string permission, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(
            IEnumerable<string> permissions,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<bool>> IsAdminAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<bool>> IsPlatformAdminAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task InvalidateAuthorizationDataCacheAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task InvalidateAuthorizationDataCacheAsync(string userId, long tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
