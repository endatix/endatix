using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

/// <summary>
/// One published version of a tenant's brand override, numbered 1..n per tenant. Rows are only appended: a publish adds
/// new content, a restore adds a copy of an older version.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class TenantBrandingVersion : ITenantOwned, IContentVersion
{
    internal TenantBrandingVersion() { } // For EF Core and TenantBranding

    /// <summary>The tenant; with <see cref="Number"/>, the primary key.</summary>
    public long TenantId { get; internal init; }

    /// <inheritdoc/>
    public long Number { get; internal init; }

    /// <inheritdoc/>
    public string Json { get; internal init; } = null!;

    /// <inheritdoc/>
    public string ContentHash { get; internal init; } = null!;

    /// <inheritdoc/>
    public DateTime CreatedAt { get; internal init; }

    /// <inheritdoc/>
    public long? CreatedBy { get; internal init; }

    /// <inheritdoc/>
    public string Source { get; internal init; } = null!;

    /// <inheritdoc/>
    public long? RestoredFromNumber { get; internal init; }
}
