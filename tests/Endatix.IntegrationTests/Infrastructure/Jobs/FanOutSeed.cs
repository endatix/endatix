using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// Seeds a <see cref="FanOutHost"/>'s database the way the app writes it, so every row raises its outbox events and
/// the host's relay and jobs see them.
/// </summary>
internal static class FanOutSeed
{
    private const string DefinitionJson =
        """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    /// <summary>A new tenant, which raises its created event.</summary>
    public static async Task<long> SeedTenantAsync(this FanOutHost host, string name, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant(name, IntegrationSeedBuilder.StableShortUrl(name));
        tenant.RaiseCreated();
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant.Id;
    }

    /// <summary>A new tenant with one form, its active one-question definition, and one completed submission.</summary>
    public static async Task<SeededSubmission> SeedSubmissionAsync(this FanOutHost host, string tenantName, CancellationToken ct)
    {
        var tenantId = await host.SeedTenantAsync(tenantName, ct);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: $"{tenantName} form"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        var definition = new FormDefinition(tenantId, isDraft: false, jsonData: DefinitionJson);
        form.AddFormDefinition(definition, isActive: false);
        await db.SaveChangesAsync(ct);
        form.SetActiveFormDefinition(definition);
        await db.SaveChangesAsync(ct);
        var submission = Submission.Create(new SubmissionCreateArgs(tenantId, form.Id, definition.Id, """{"q1":"hello"}"""));
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);
        return new SeededSubmission(tenantId, form.Id, submission.Id);
    }
}

/// <summary>A seeded submission and the tenant and form it belongs to.</summary>
internal sealed record SeededSubmission(long TenantId, long FormId, long SubmissionId);
