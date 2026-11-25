using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>The signed-in subject whose pending actions are being evaluated.</summary>
/// <param name="UserId">Canonical user identifier from authorization data.</param>
/// <param name="TenantId">Tenant in which the user is acting.</param>
/// <param name="Roles">Roles resolved for the user in that tenant.</param>
[Experimental("ENDATIX0006")]
public sealed record RequiredActionSubject(string UserId, long TenantId, IReadOnlyCollection<string> Roles);

/// <summary>Reports pending actions when authorization data is built for a signed-in user.</summary>
/// <remarks>Each concurrent invocation has its own dependency-injection scope. Use <see cref="RequiredActionSubject"/>
/// for the caller's identity, tenant and roles; request-initialized scoped state is not available here.
/// A provider reading tenant-owned data must explicitly bind the read to the subject's positive tenant ID.
/// An ambient tenant of zero is not isolation. If a mandatory provider cannot establish tenant standing,
/// it must fail the check rather than return an empty list.
/// The token belongs to this invocation. The host signals it on caller cancellation, timeout, or when
/// <see cref="GetPendingAsync"/> completes (including success), whichever happens first. Signaling at
/// successful completion does not invalidate the report or cancel the request. The host waits for token
/// callbacks before disposing the invocation scope. Do not retain scoped dependencies beyond the invocation.
/// The caller deadline includes cleanup; late reports are discarded.
/// While an invocation or its cleanup is outstanding, the host will not start another for the same
/// user, tenant and registration, even after a refresh. Other users and registrations can proceed.
/// Keep DI factories, constructors and configuration properties local and free of I/O: the host needs
/// the policy before it can enforce a timeout or decide how to handle an unavailable remote service.
/// Perform remote checks only inside <see cref="GetPendingAsync"/>.</remarks>
[Experimental("ENDATIX0006")]
public interface IRequiredActionProvider
{
    /// <summary>A stable, non-secret revision of the settings and behavior that determine this provider's reported actions.</summary>
    /// <remarks>Use the same revision on hosts with equivalent configuration. Change it when their snapshots are no
    /// longer interchangeable. Do not include credentials, customer identifiers, document text or raw settings.
    /// This is not a per-request key: changes in a subject's standing use normal access-version and expiry rules.</remarks>
    string CacheConfigurationKey { get; }

    /// <summary>Maximum time to wait for this provider during hydration.</summary>
    TimeSpan Timeout => TimeSpan.FromSeconds(2);

    /// <summary>How long a successful report is fresh, capped at 60 seconds. Optional fallback never uses a report older than 60 seconds.</summary>
    TimeSpan CacheFor => TimeSpan.FromSeconds(30);

    /// <summary>Report unavailable standing rather than admitting a subject without a fresh answer.</summary>
    bool FailClosed => false;

    /// <summary>Returns only outstanding actions applicable to the subject; an empty list means none.</summary>
    /// <remarks>Reports sharing a key must describe the same obligation. Role order, repeated roles and time-zone
    /// offsets do not distinguish reports. A contradiction makes the subject's required-action standing unavailable;
    /// registration order never selects a winner.</remarks>
    Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken);
}
