using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// FluentValidation rules for UTC calendar day range properties.
/// </summary>
public sealed class CreatedRangeRequestValidator : AbstractValidator<ICreatedRange>
{
    public CreatedRangeRequestValidator()
    {
        RuleFor(x => x.CreatedFrom).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x.CreatedTo).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x)
            .Must(range => UtcCalendarDay.IsFromOnOrBeforeTo(range.CreatedFrom, range.CreatedTo))
            .WithMessage("CreatedFrom must be on or before CreatedTo.")
            .WithName("CreatedFrom");
    }
}

/// <summary>
/// FluentValidation rules for modified-at calendar day range properties.
/// </summary>
public sealed class ModifiedRangeRequestValidator : AbstractValidator<IModifiedRange>
{
    public ModifiedRangeRequestValidator()
    {
        RuleFor(x => x.ModifiedFrom).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x.ModifiedTo).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x)
            .Must(range => UtcCalendarDay.IsFromOnOrBeforeTo(range.ModifiedFrom, range.ModifiedTo))
            .WithMessage("ModifiedFrom must be on or before ModifiedTo.")
            .WithName("ModifiedFrom");
    }
}

/// <summary>
/// FluentValidation rules for started-at calendar day range properties.
/// </summary>
public sealed class StartedRangeRequestValidator : AbstractValidator<IStartedRange>
{
    public StartedRangeRequestValidator()
    {
        RuleFor(x => x.StartedFrom).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x.StartedTo).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x)
            .Must(range => UtcCalendarDay.IsFromOnOrBeforeTo(range.StartedFrom, range.StartedTo))
            .WithMessage("StartedFrom must be on or before StartedTo.")
            .WithName("StartedFrom");
    }
}

/// <summary>
/// FluentValidation rules for completed-at calendar day range properties.
/// </summary>
public sealed class CompletedRangeRequestValidator : AbstractValidator<ICompletedRange>
{
    public CompletedRangeRequestValidator()
    {
        RuleFor(x => x.CompletedFrom).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x.CompletedTo).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x)
            .Must(range => UtcCalendarDay.IsFromOnOrBeforeTo(range.CompletedFrom, range.CompletedTo))
            .WithMessage("CompletedFrom must be on or before CompletedTo.")
            .WithName("CompletedFrom");
    }
}

/// <summary>
/// FluentValidation rules for last-login calendar day range properties.
/// </summary>
public sealed class LastLoginRangeRequestValidator : AbstractValidator<ILastLoginRange>
{
    public LastLoginRangeRequestValidator()
    {
        RuleFor(x => x.LastLoginFrom).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x.LastLoginTo).MustBeUtcCalendarDateWhenPresent();
        RuleFor(x => x)
            .Must(range => UtcCalendarDay.IsFromOnOrBeforeTo(range.LastLoginFrom, range.LastLoginTo))
            .WithMessage("LastLoginFrom must be on or before LastLoginTo.")
            .WithName("LastLoginFrom");
    }
}
