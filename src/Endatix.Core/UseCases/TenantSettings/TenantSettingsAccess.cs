using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.TenantSettings;

/// <summary>Checks access before an account operation reads or changes tenant settings.</summary>
[Experimental("ENDATIX0006")]
public static class TenantSettingsAccess
{
    /// <summary>Check access to read settings.</summary>
    public sealed record Read : IQuery<Result>;

    /// <summary>Check access to update settings.</summary>
    public sealed record Update : IQuery<Result>;
}

internal sealed class TenantSettingsAccessHandler(
    IRepository<Entities.TenantSettings> settings, ITenantContext tenant, AccountStanding standing)
    : IQueryHandler<TenantSettingsAccess.Read, Result>, IQueryHandler<TenantSettingsAccess.Update, Result>
{
    public Task<Result> Handle(TenantSettingsAccess.Read request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenant.TenantId);
        return CheckAsync(cancellationToken);
    }

    public Task<Result> Handle(TenantSettingsAccess.Update request, CancellationToken cancellationToken) => tenant.TenantId > 0
        ? CheckAsync(cancellationToken)
        : Task.FromResult(MissingSettings());

    private async Task<Result> CheckAsync(CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return Result.Success();
        }

        return await settings.AnyAsync(new TenantSettingsByTenantIdSpec(tenant.TenantId), ct)
            ? await standing.RefusalAsync(ct) ?? Result.Success()
            : MissingSettings();
    }

    private static Result MissingSettings() => Result.NotFound("Tenant settings not found.");
}
