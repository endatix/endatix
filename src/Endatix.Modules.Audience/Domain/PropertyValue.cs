using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One property cell on a form membership.
/// </summary>
public sealed class PropertyValue : BaseEntity, IAggregateRoot, ITenantOwned
{
    public const int VALUE_MAX_LENGHT = 4000;
    private PropertyValue()
    {
        Value = string.Empty;
    }

    public PropertyValue(PropertyValueCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.MembershipId);
        Guard.Against.NegativeOrZero(args.PropertyId);
        DomainValidationException.ThrowIfError(ValueError(args.Value), nameof(args));

        TenantId = args.TenantId;
        MembershipId = args.MembershipId;
        PropertyId = args.PropertyId;
        Value = args.Value;
    }

    public long TenantId { get; private set; }

    public long MembershipId { get; private set; }

    public long PropertyId { get; private set; }

    /// <summary>Canonical string. Multiple choice is a JSON array of choice keys.</summary>
    public string Value { get; private set; }

    public void SetValue(string value)
    {
        DomainValidationException.ThrowIfError(ValueError(value), nameof(value));
        Value = value;
    }

    public static string? ValueError(string? value)
    {
        if (value is null)
        {
            return "Value is required.";
        }

        return value.Length > VALUE_MAX_LENGHT
            ? $"Value must be at most {VALUE_MAX_LENGHT} characters."
            : null;
    }
}
