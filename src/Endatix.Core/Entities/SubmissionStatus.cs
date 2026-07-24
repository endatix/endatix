using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

/// <summary>Wire/persistence codes for built-in submission statuses.</summary>
public static class SubmissionStatusCodes
{
    public const string New = "new";
    public const string Approved = "approved";
    public const string Read = "read";
    public const string Declined = "declined";
}

public sealed record SubmissionStatus : IComparable<SubmissionStatus>
{
    public const int STATUS_CODE_MAX_LENGTH = 16;

    // Required by EF Core
    private SubmissionStatus()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    /// <summary>
    /// Catalog value for comparisons. Do not assign catalog instances to aggregates —
    /// use <see cref="FromCode(string)"/> / <see cref="CreateInstance"/> so each entity
    /// gets a distinct owned instance for EF tracking.
    /// </summary>
    public static readonly SubmissionStatus New = new("New", SubmissionStatusCodes.New);

    public static readonly SubmissionStatus Approved = new("Approved", SubmissionStatusCodes.Approved);
    public static readonly SubmissionStatus Read = new("Read", SubmissionStatusCodes.Read);

    public static readonly SubmissionStatus Declined = new("Declined", SubmissionStatusCodes.Declined);

    public string Name { get; }
    public string Code { get; }

    private SubmissionStatus(string name, string code)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(code);

        Name = name;
        Code = code.ToLowerInvariant();
    }

    /// <summary>
    /// Fresh instance for EF <c>OwnsOne</c> tracking (same value, different object identity).
    /// </summary>
    public SubmissionStatus CreateInstance() => this with { };

    /// <summary>
    /// Resolves a status code to a fresh instance suitable for persistence.
    /// Prefer <see cref="SubmissionStatusCodes"/> over raw literals.
    /// </summary>
    public static SubmissionStatus FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code, nameof(code));

        var catalog = code.ToLowerInvariant() switch
        {
            SubmissionStatusCodes.New => New,
            SubmissionStatusCodes.Approved => Approved,
            SubmissionStatusCodes.Read => Read,
            SubmissionStatusCodes.Declined => Declined,
            _ => throw new ArgumentException($"Invalid status code: {code}", nameof(code))
        };

        return catalog.CreateInstance();
    }

    public override string ToString() => Name;

    public int CompareTo(SubmissionStatus? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.Compare(Code, other.Code, StringComparison.Ordinal);
    }
}
