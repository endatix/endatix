using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Makes rebuilds of one form's schema run one after another: a PostgreSQL advisory lock held until the transaction
/// that took it ends, so a rebuild that waited reads what the one before it saved.
/// </summary>
/// <remarks>
/// <para>
/// The key is the first 8 bytes of SHA-256 over a scope name, the tenant id and the form id. The scope keeps it apart
/// from any other one-key advisory lock taken on the same ids, and PostgreSQL never matches a one-key lock with the
/// two-key locks other modules take. Two forms whose keys collide only wait for each other; neither loses columns.
/// </para>
/// <para>
/// Reporting runs on PostgreSQL only, so another provider is refused rather than left to rebuild without the lock.
/// </para>
/// </remarks>
internal static class FormSchemaRebuildLock
{
    private const string Scope = "endatix.reporting.form-schema";

    /// <summary>Waits until no other transaction holds the form's lock, then holds it until this one ends.</summary>
    /// <exception cref="NotSupportedException">The database is not PostgreSQL.</exception>
    /// <exception cref="InvalidOperationException">No transaction is open, so the lock would end with the statement.</exception>
    public static async Task AcquireAsync(
        DatabaseFacade database,
        FormSchemaLockTarget target,
        CancellationToken cancellationToken)
    {
        if (!database.IsNpgsql())
        {
            throw new NotSupportedException(
                $"Form schema rebuilds need PostgreSQL; the database provider is '{database.ProviderName}'.");
        }

        if (database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The form schema rebuild lock must be taken inside a transaction.");
        }

        var key = KeyFor(target);
        await database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    internal static long KeyFor(FormSchemaLockTarget target)
    {
        var name = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{Scope}:{target.TenantId}:{target.FormId}"));
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(name, hash);
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }
}

/// <summary>The form whose schema a rebuild lock guards.</summary>
internal readonly record struct FormSchemaLockTarget(long TenantId, long FormId);
