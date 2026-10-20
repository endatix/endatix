using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A form's lock held past its wait: the request that waited is refused with a retryable 503 and writes nothing,
/// instead of holding its connection until the command times out.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    [Fact]
    public async Task Creating_a_submission_while_the_form_is_held_for_deletion_is_unavailable_without_waiting()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var deleting = FormSubmissionsLock.Adding(formId) with { Mode = TransactionLockMode.Exclusive };
        await using var held = await HoldAsync(world.Services, deleting);

        // Act
        var started = Stopwatch.StartNew();
        using var response = await admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct);
        var took = started.Elapsed;

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await DeletedByIdAsync(world.Services, formId));
        Assert.True(took < WaitProbe, $"The create waited {took} for the form's deletion.");
    }

    [Fact]
    public async Task Deleting_a_form_while_a_create_holds_it_too_long_is_unavailable()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var submissionId = await CreateSubmissionAsync(admin, formId);
        await using var held = await HoldAsync(world.Services, FormSubmissionsLock.Adding(formId));

        // Act
        using var response = await admin.DeleteAsync($"/api/forms/{formId}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(new Dictionary<long, bool> { [submissionId] = false }, await DeletedByIdAsync(world.Services, formId));
    }

    /// <summary>
    /// Form ids are public. A delete of another tenant's form is not found before it takes the lock, so it never holds
    /// up that form's creates; here a create under way holds the lock, and the delete does not wait for it.
    /// </summary>
    [Fact]
    public async Task Deleting_another_tenants_form_is_not_found_without_waiting_for_its_lock()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            Ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: Ct);
        using var stranger = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: Ct);
        var formId = await CreateFormAsync(owner);
        await using var held = await HoldAsync(world.Services, FormSubmissionsLock.Adding(formId));

        // Act
        using var response = await stranger.DeleteAsync($"/api/forms/{formId}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await DeletedByIdAsync(world.Services, formId));
    }

    /// <summary>The lock, held by the test in a scope of its own until disposed.</summary>
    private static async Task<HeldLock> HoldAsync(IServiceProvider services, TransactionLockRequest request)
    {
        var scope = services.CreateAsyncScope();
        try
        {
            var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transaction = await scope.ServiceProvider.GetRequiredService<ITransactionLock>()
                .BeginLockedTransactionAsync(app.Database, request, Ct);
            return new HeldLock(scope, transaction);
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    private sealed class HeldLock(AsyncServiceScope scope, IAsyncDisposable transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }
}
