using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Validates <see cref="ICreatedRange"/> calendar days (format + From on or before To).
/// </summary>
public sealed class CreatedRangeValidator : AbstractValidator<ICreatedRange>
{
    public CreatedRangeValidator() =>
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, nameof(ICreatedRange.CreatedFrom));
}

/// <summary>
/// Validates <see cref="IModifiedRange"/> calendar days (format + From on or before To).
/// </summary>
public sealed class ModifiedRangeValidator : AbstractValidator<IModifiedRange>
{
    public ModifiedRangeValidator() =>
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, nameof(IModifiedRange.ModifiedFrom));
}

/// <summary>
/// Validates <see cref="IStartedRange"/> calendar days (format + From on or before To).
/// </summary>
public sealed class StartedRangeValidator : AbstractValidator<IStartedRange>
{
    public StartedRangeValidator() =>
        this.RuleForCalendarDayRange(x => x.StartedFrom, x => x.StartedTo, nameof(IStartedRange.StartedFrom));
}

/// <summary>
/// Validates <see cref="ICompletedRange"/> calendar days (format + From on or before To).
/// </summary>
public sealed class CompletedRangeValidator : AbstractValidator<ICompletedRange>
{
    public CompletedRangeValidator() =>
        this.RuleForCalendarDayRange(x => x.CompletedFrom, x => x.CompletedTo, nameof(ICompletedRange.CompletedFrom));
}

/// <summary>
/// Validates <see cref="ILastLoginRange"/> calendar days (format + From on or before To).
/// </summary>
public sealed class LastLoginRangeValidator : AbstractValidator<ILastLoginRange>
{
    public LastLoginRangeValidator() =>
        this.RuleForCalendarDayRange(x => x.LastLoginFrom, x => x.LastLoginTo, nameof(ILastLoginRange.LastLoginFrom));
}
