using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A form's lock held past its wait: the request that waited is refused with a retryable 503 and writes nothing,
/// instead of holding its connection until the command times out.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    [Fact]
    public async Task Creating_a_submission_while_the_form_is_held_for_deletion_too_long_is_unavailable()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var deleting = new TransactionLockRequest(
            TransactionLockScopes.FormSubmissions,
            formId.ToString(CultureInfo.InvariantCulture));
        await using var held = await HoldAsync(world.Services, deleting);

        // Act
        using var response = await admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await DeletedByIdAsync(world.Services, formId));
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
