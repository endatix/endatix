using System.Text.RegularExpressions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Delivering the outbox in the test: refused while a relay holds messages it cannot claim, and a handler's failure
/// leaves its batch claimable for the next delivery.
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

    private const string LostConnection = "The connection was lost.";

    [GeneratedRegex(@"TimeoutException: The connection was lost\.")]
    private static partial Regex LostConnectionPattern();

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
