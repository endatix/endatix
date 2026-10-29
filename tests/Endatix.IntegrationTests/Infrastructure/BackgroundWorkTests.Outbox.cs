extern alias EndatixWebHost;

using System.Text.RegularExpressions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests;

/// <summary>
/// Delivering the outbox in the test: refused while a relay holds messages it cannot claim, waited for by a stop, and a
/// handler's or the store's failure leaves its batch claimable for the next delivery.
/// </summary>
public sealed partial class BackgroundWorkTests
{
    [Fact]
    public async Task Delivering_the_outbox_in_the_test_while_the_relay_is_on_is_refused()
    {
        // Arrange — reset first, so the relay meets no message an earlier test left.
        await fixture.PrepareWorldAsync(IntegrationWorldOptions.Empty, Ct);
        fixture.BackgroundWork.StartRelay();

        // Act
        var refused = await Record.ExceptionAsync(() => fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct));

        // Assert
        Assert.IsType<InvalidOperationException>(refused);
    }

    [Fact]
    public async Task Delivering_the_outbox_in_the_test_while_a_relay_batch_outlives_its_stop_is_refused()
    {
        // Arrange — the relay is held in a handler's transaction, and the stop gives up waiting for it.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var handler = Gate.PauseAt(AudienceStatements.MembershipsSoftDelete());
        fixture.BackgroundWork.StartRelay();
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        await handler.Reached.WaitAsync(Patience, Ct);
        using var giveUp = new CancellationTokenSource(StopProbe);
        await Record.ExceptionAsync(() => fixture.BackgroundWork.StopAsync(giveUp.Token));

        // Act
        var refused = await Record.ExceptionAsync(() => fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct));

        // Assert
        Assert.IsType<InvalidOperationException>(refused);
    }

    [Fact]
    public async Task A_handler_that_fails_fails_the_delivery_with_its_own_error_and_the_next_delivery_sends_the_batch()
    {
        // Arrange — two deleted forms; the first form's people cleanup fails once, as a lost connection would.
        var admin = await TenantAdminAsync();
        var first = await CreateFormAsync(admin);
        var second = await CreateFormAsync(admin);
        using var firstDeleted = await admin.DeleteAsync($"/api/forms/{first}", Ct);
        using var secondDeleted = await admin.DeleteAsync($"/api/forms/{second}", Ct);
        using var broken = Gate.FailAt(AudienceStatements.MembershipsSoftDelete(), new TimeoutException(LostConnection));

        // Act
        var failure = await Record.ExceptionAsync(() => fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct));
        var retry = await Record.ExceptionAsync(() => fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct));

        // Assert
        Assert.Matches(LostConnectionPattern(), failure?.ToString() ?? string.Empty);
        Assert.Null(retry);
        Assert.All(await OutboxStatusesAsync(), status => Assert.Equal(OutboxMessageStatus.Sent, status));
    }

    [Fact]
    public async Task Stopping_waits_for_a_delivery_in_the_test_whose_handler_is_running()
    {
        // Arrange — the test's own delivery runs the form's people cleanup, held at its second statement.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var handler = Gate.PauseAt(AudienceStatements.MembershipsSoftDelete());
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        var delivering = fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct);
        await handler.Reached.WaitAsync(Patience, Ct);

        // Act
        var stopping = fixture.BackgroundWork.StopAsync(Ct);
        var stoppedWhileHeld = await Task.WhenAny(stopping, Task.Delay(StopProbe, Ct)) == stopping;
        handler.Release();
        await delivering.WaitAsync(Patience, Ct);
        await stopping.WaitAsync(Patience, Ct);

        // Assert
        Assert.False(stoppedWhileHeld, "The stop returned while the delivery's handler was still in its transaction.");
    }

    [Fact]
    public async Task A_store_that_fails_to_mark_a_message_sent_fails_the_delivery_and_the_next_delivery_sends_the_batch()
    {
        // Arrange — a host on the shared database whose outbox store fails its first mark, as a lost connection would.
        var admin = await TenantAdminAsync();
        using var firstDeleted = await admin.DeleteAsync($"/api/forms/{await CreateFormAsync(admin)}", Ct);
        using var secondDeleted = await admin.DeleteAsync($"/api/forms/{await CreateFormAsync(admin)}", Ct);
        await using var host = HostWhoseFirstMarkFails();
        var background = BackgroundWork.Of(host.Services);

        // Act
        var failure = await Record.ExceptionAsync(() => background.DeliverOutboxOnceAsync(Ct));
        var retry = await Record.ExceptionAsync(() => background.DeliverOutboxOnceAsync(Ct));

        // Assert
        Assert.Matches(LostConnectionPattern(), failure?.ToString() ?? string.Empty);
        Assert.Null(retry);
        Assert.All(await OutboxStatusesAsync(), status => Assert.Equal(OutboxMessageStatus.Sent, status));
    }

    private WebApplicationFactory<EndatixWebHost::Program> HostWhoseFirstMarkFails()
    {
        var failures = new MarkFailures(1);
        var host = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Scoped<IOutboxClaimStore>(provider => new MarkFailingClaimStore(
                    ActivatorUtilities.CreateInstance<SqlOutboxClaimStore>(provider), failures)))));
        return FailedHostStart.DisposeOnFailure(
            () =>
            {
                _ = host.Services;
                return host;
            },
            host.Dispose);
    }

    private const string LostConnection = "The connection was lost.";

    [GeneratedRegex(@"TimeoutException: The connection was lost\.")]
    private static partial Regex LostConnectionPattern();

    /// <summary>How many more marks fail.</summary>
    private sealed class MarkFailures(int count)
    {
        private int _left = count;

        public bool TakeOne() => Interlocked.Decrement(ref _left) >= 0;
    }

    /// <summary>The host's outbox store, failing marks while <see cref="MarkFailures"/> has any left.</summary>
    private sealed class MarkFailingClaimStore(IOutboxClaimStore store, MarkFailures failures) : IOutboxClaimStore
    {
        public Task<IReadOnlyList<IOutboxMessage>> ClaimBatchAsync(
            string instanceId, TimeSpan lease, int batchSize, CancellationToken cancellationToken) =>
            store.ClaimBatchAsync(instanceId, lease, batchSize, cancellationToken);

        public Task MarkSentAsync(IOutboxMessage message, string instanceId, CancellationToken cancellationToken) =>
            failures.TakeOne()
                ? throw new TimeoutException(LostConnection)
                : store.MarkSentAsync(message, instanceId, cancellationToken);

        public Task RescheduleAsync(
            IOutboxMessage message, DateTimeOffset nextAttemptAt, string instanceId, CancellationToken cancellationToken) =>
            store.RescheduleAsync(message, nextAttemptAt, instanceId, cancellationToken);

        public Task MarkFailedAsync(IOutboxMessage message, string instanceId, CancellationToken cancellationToken) =>
            store.MarkFailedAsync(message, instanceId, cancellationToken);
    }

    private async Task<List<OutboxMessageStatus>> OutboxStatusesAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(message => message.Status)
            .ToListAsync(Ct);
    }
}
