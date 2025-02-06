using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

public sealed record SubmissionStatus : IComparable<SubmissionStatus>
{
    public const int STATUS_CODE_MAX_LENGTH = 16;
    
    // Required by EF Core
    private SubmissionStatus()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    public static readonly SubmissionStatus New = new("New", "new");

    public static readonly SubmissionStatus Approved = new("Approved", "approved");
    public static readonly SubmissionStatus Read = new("Read", "read");

    public static readonly SubmissionStatus Declined = new("Declined", "declined");

    public string Name { get; }
    public string Code { get; }

    private SubmissionStatus(string name, string code)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(code, nameof(code));

        Name = name;
        Code = code.ToLowerInvariant();
    }

    public static SubmissionStatus FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code, nameof(code));

        var status = code.ToLowerInvariant() switch
        {
            "new" => New,
            "approved" => Approved,
            "read" => Read,
            "declined" => Declined,
            _ => throw new ArgumentException($"Invalid status code: {code}", nameof(code))
        };

        return status;
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