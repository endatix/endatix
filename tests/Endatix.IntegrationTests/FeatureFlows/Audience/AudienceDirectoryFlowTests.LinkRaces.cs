using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Opens of one personalised link that overlap, forced with a gate that pauses the first open at
/// its insert, or at its first read of a submission, while it holds the link's lock.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>
    /// The interleaving behind #1204 and #1210, forced instead of hoped for: the first open holds
    /// the link's lock and is paused at its insert when a second open arrives. The second waits,
    /// then resumes the first one's submission with the same access token.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_while_its_first_open_is_inserting_waits_and_returns_the_same_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Canton");
        using var anonymous = world.AnonymousClient();
        var gate = CommandGate.AtInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gatedApp = GatedAppDbContext(scope.ServiceProvider, gate);
        await using var firstOpen = PausedOpen(
            gate,
            HandlerWith(scope.ServiceProvider, gatedApp),
            new RedeemLinkCommand(formId, token));
        await firstOpen.ReachedGateAsync();

        var audienceConnection = ((DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>())
            .Database.GetDbConnection();

        // Act
        var secondOpen = firstOpen.Alongside(RedeemAsync(anonymous, formId, token));
        var sharedConnection = ReferenceEquals(audienceConnection, gatedApp.Database.GetDbConnection());
        var secondWaited = await Task.WhenAny(secondOpen, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != secondOpen;
        var first = await firstOpen.ReleaseAsync();
        var second = await secondOpen.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(secondWaited, "The second open did not wait for the first open's lock.");
        Assert.True(sharedConnection, "The locked section rented a second connection.");
        Assert.True(first.IsSuccess);
        Assert.True(first.Value.Created);
        Assert.False(second.GetProperty("created").GetBoolean());
        Assert.Equal(first.Value.SubmissionId, IdOf(second, "submissionId"));
        Assert.Equal(first.Value.AccessToken, AccessTokenOf(second));
        await AssertFillsInAsync(anonymous, (formId, first.Value.SubmissionId), first.Value.SubmissionAccessToken);
        await AssertOnlySubmissionAsync(world.Services, formId, first.Value.SubmissionId);
    }

    /// <summary>
    /// A writer that claims without the lock (a pod on the previous version during a deploy) wins the
    /// claim while this open is paused at its insert. This open rolls its submission back and resumes
    /// the one the link names.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_that_another_writer_claimed_first_resumes_that_writers_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Mentor");
        var gate = CommandGate.AtInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gatedApp = GatedAppDbContext(scope.ServiceProvider, gate);
        await using var firstOpen = PausedOpen(
            gate,
            HandlerWith(scope.ServiceProvider, gatedApp),
            new RedeemLinkCommand(formId, token));
        await firstOpen.ReachedGateAsync();
        var otherWritersId = await ClaimWithoutTheLockAsync(world.Services, formId);

        // Act
        var opened = await firstOpen.ReleaseAsync();

        // Assert
        Assert.True(opened.IsSuccess);
        Assert.False(opened.Value.Created);
        Assert.Equal(otherWritersId, opened.Value.SubmissionId);
        await AssertOnlySubmissionAsync(world.Services, formId, otherWritersId);
    }

    /// <summary>
    /// An on-behalf create for the same person commits while this open, on a one-response form, is paused at its
    /// insert. <c>UX_Submissions_RestrictionKey</c> refuses this open's insert, and the open takes the on-behalf
    /// submission over instead of leaving the person with two.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_while_an_on_behalf_create_for_the_person_commits_takes_that_submission_over()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Parma", limitOnePerUser: true);
        var gate = CommandGate.AtInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gatedApp = GatedAppDbContext(scope.ServiceProvider, gate);
        await using var firstOpen = PausedOpen(
            gate,
            HandlerWith(scope.ServiceProvider, gatedApp),
            new RedeemLinkCommand(link.FormId, link.Token));
        await firstOpen.ReachedGateAsync();
        using var created = await OnBehalfAsync(admin, link.FormId, link.Identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var onBehalfId = IdOf(await admin.ReadAsync(created), "id");

        // Act
        var opened = await firstOpen.ReleaseAsync();

        // Assert
        Assert.True(opened.IsSuccess);
        Assert.False(opened.Value.Created);
        Assert.Equal(onBehalfId, opened.Value.SubmissionId);
        await AssertOnlySubmissionAsync(world.Services, link.FormId, onBehalfId);
    }

    /// <summary>
    /// The person is removed after the open passed <see cref="LinkOpenGates"/>, while it holds the lock. The open
    /// would take over their on-behalf submission, which already has values; the check under the lock refuses it.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_for_a_person_removed_after_its_gate_check_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Elyria", limitOnePerUser: true);
        using var created = await OnBehalfAsync(admin, link.FormId, link.Identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var gate = CommandGate.AtFirstRead();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gatedApp = GatedAppDbContext(scope.ServiceProvider, gate);
        await using var open = PausedOpen(
            gate,
            HandlerWith(scope.ServiceProvider, gatedApp),
            new RedeemLinkCommand(link.FormId, link.Token));
        await open.ReachedGateAsync();
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        var opened = await open.ReleaseAsync();

        // Assert
        Assert.Equal(ResultStatus.NotFound, opened.Status);
        Assert.Null(await LinkSubmissionIdAsync(world.Services, link.FormId));
    }

    /// <summary>
    /// Before the lock, the claim committed ahead of the token, so a link can name a submission
    /// without one. Parallel opens of it issue that token once.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_claimed_without_a_token_many_times_at_once_issues_one_access_token()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Elyria");
        using var anonymous = world.AnonymousClient();
        var submissionId = IdOf(await RedeemAsync(anonymous, formId, token), "submissionId");
        await ClearTokenAsync(world.Services, submissionId);

        // Act
        var opened = await Task.WhenAll(
            Enumerable.Range(0, ParallelOpens).Select(_ => RedeemAsync(anonymous, formId, token)));
        var reopened = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.All(opened, item => Assert.Equal(submissionId, IdOf(item, "submissionId")));
        Assert.All(opened, item => Assert.Equal(AccessTokenOf(reopened), AccessTokenOf(item)));
    }

    [Fact]
    public async Task Opening_a_link_while_another_open_holds_it_too_long_is_unavailable()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Lorain");
        using var anonymous = world.AnonymousClient();
        await using var holder = world.Services.CreateAsyncScope();
        await using var held = await HoldLinkLockAsync(holder.ServiceProvider, formId);

        // Act
        using var response = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>What an open without the lock leaves: its own submission, and the link claimed with it.</summary>
    private static async Task<long> ClaimWithoutTheLockAsync(IServiceProvider services, long formId)
    {
        var submissionId = await LeaveUnclaimedSubmissionAsync(services, formId);
        await using var scope = services.CreateAsyncScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var updated = await audience.Links
            .Where(item => item.FormId == formId && item.SubmissionId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.SubmissionId, submissionId)
                    .SetProperty(item => item.OpenedAt, DateTime.UtcNow),
                TestContext.Current.CancellationToken);
        Assert.Equal(1, updated);
        return submissionId;
    }

    private static async Task ClearTokenAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await app.Submissions
            .Where(item => item.Id == submissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Token!.Value, (string?)null)
                    .SetProperty(item => item.Token!.ExpiresAt, (DateTime?)null),
                TestContext.Current.CancellationToken);
        Assert.Equal(1, updated);
    }

    /// <summary>The link's open lock, held by the test until disposed.</summary>
    private static async Task<IAsyncDisposable> HoldLinkLockAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var audience = services.GetRequiredService<IAudienceDbContext>();
        var linkId = await audience.Links.AsNoTracking()
            .Where(item => item.FormId == formId)
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        return await services.GetRequiredService<ITransactionLock>().BeginLockedTransactionAsync(
            ((DbContext)audience).Database,
            new TransactionLockRequest(
                TransactionLockScopes.AudienceLinkOpen,
                linkId.ToString(CultureInfo.InvariantCulture)),
            cancellationToken);
    }

    private static readonly TimeSpan _gateTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Long enough for an open that does not wait to finish; the real one waits until released.</summary>
    private static readonly TimeSpan SecondOpenProbe = TimeSpan.FromSeconds(2);

    /// <summary>The host's <see cref="AppDbContext"/> options plus the gate, so only this request pauses.</summary>
    private static AppDbContext GatedAppDbContext(IServiceProvider services, IInterceptor gate)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>(
                services.GetRequiredService<DbContextOptions<AppDbContext>>())
            .AddInterceptors(gate)
            .Options;
        return new AppDbContext(
            options,
            services.GetRequiredService<ITenantContext>(),
            services.GetRequiredService<OutboxIntegrationEventDispatcher>());
    }

    /// <summary>The host's handler, with every <see cref="AppDbContext"/> read and write going through <paramref name="app"/>.</summary>
    private static RedeemLinkHandler HandlerWith(IServiceProvider services, AppDbContext app)
    {
        var evaluator = services.GetRequiredService<EndatixSpecificationEvaluator>();
        var audience = services.GetRequiredService<IAudienceDbContext>();
        var forms = new EfRepository<Form>(app, evaluator);
        var submissions = new EfRepository<Submission>(app, evaluator);
        var tokens = new SubmissionTokenService(submissions, forms, new EfRepository<TenantSettings>(app, evaluator));
        var linkLock = new LinkOpenLock(audience, app, services.GetRequiredService<ITransactionLock>());
        var gates = new LinkOpenGates(
            audience,
            forms,
            new EfRepository<Submitter>(app, evaluator),
            services.GetRequiredService<ISubmitterResolver>());
        var linkTokens = new LinkTokens(services.GetRequiredService<ISubmissionAccessTokenService>(), tokens);
        return new RedeemLinkHandler(audience, submissions, app, linkTokens, linkLock, gates);
    }

    /// <summary>Starts a link open paused at <paramref name="gate"/>.</summary>
    private static PausedRace<Result<RedeemedLinkDto>> PausedOpen(
        CommandGate gate,
        RedeemLinkHandler handler,
        RedeemLinkCommand open) =>
        new(gate, handler.Handle(open, TestContext.Current.CancellationToken));

    /// <summary>
    /// A handler held at its gate, and the requests made alongside it. Disposing releases it and waits for it and for
    /// the requests made alongside it, so a failed assert never leaves one running into the next test.
    /// </summary>
    private sealed class PausedRace<T>(CommandGate gate, Task<T> paused) : IAsyncDisposable
    {
        private readonly List<Task> _companions = [];

        public async Task ReachedGateAsync()
        {
            await Task.WhenAny(gate.Reached, paused).WaitAsync(_gateTimeout, TestContext.Current.CancellationToken);
            Assert.True(gate.Reached.IsCompleted, "The paused handler never reached its gate.");
        }

        public Task<T> ReleaseAsync()
        {
            gate.Release();
            return paused.WaitAsync(_gateTimeout, TestContext.Current.CancellationToken);
        }

        /// <summary>A request made while the handler is paused; disposing drains it too.</summary>
        public Task<TCompanion> Alongside<TCompanion>(Task<TCompanion> companion)
        {
            _companions.Add(companion);
            return companion;
        }

        public async ValueTask DisposeAsync()
        {
            gate.Release();
            await ((Task)paused).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            foreach (var companion in _companions)
            {
                await companion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    /// <summary>
    /// Holds the first matching command until the test releases it: on <c>Submissions</c> for the link races, or on a
    /// participant's cells and membership for the edit, import and removal races. Without a statement, it holds the
    /// first commit instead, after every write of the transaction ran.
    /// </summary>
    private sealed partial class CommandGate(Regex? statement) : DbCommandInterceptor, IDbTransactionInterceptor
    {
        /// <summary>The open's insert of its new submission.</summary>
        public static CommandGate AtInsert() => new(SubmissionInsert());

        /// <summary>
        /// The open's first read of a submission: under the lock, after <see cref="LinkOpenGates"/>, which reads none.
        /// </summary>
        public static CommandGate AtFirstRead() => new(SubmissionRead());

        /// <summary>An edit's or an import's insert of a participant's value cell.</summary>
        public static CommandGate AtCellInsert() => new(CellInsert());

        /// <summary>A removal's soft delete of the participant's membership.</summary>
        public static CommandGate AtMembershipRemoval() => new(MembershipUpdate());

        /// <summary>The commit of the gated context's transaction: its rows are written, and held by its locks.</summary>
        public static CommandGate AtCommit() => new(null);

        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => _reached.Task;

        public void Release() => _released.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await HoldAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await HoldAsync(command, cancellationToken);
            return result;
        }

        public async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (statement is null && _reached.TrySetResult())
            {
                await _released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }

        /// <summary>PostgreSQL quotes the table <c>"Submissions"</c>, SQL Server <c>[Submissions]</c>.</summary>
        [GeneratedRegex("""INSERT INTO ["\[]Submissions["\]]""")]
        private static partial Regex SubmissionInsert();

        [GeneratedRegex("""FROM ["\[]Submissions["\]]""")]
        private static partial Regex SubmissionRead();

        /// <summary>The audience schema qualifies these: <c>audience."PropertyValues"</c>, <c>[audience].[PropertyValues]</c>.</summary>
        [GeneratedRegex("""INSERT INTO \S*["\[]PropertyValues["\]]""")]
        private static partial Regex CellInsert();

        [GeneratedRegex("""UPDATE \S*["\[]Memberships["\]]""")]
        private static partial Regex MembershipUpdate();

        private async Task HoldAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (statement?.IsMatch(command.CommandText) == true && _reached.TrySetResult())
            {
                await _released.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
