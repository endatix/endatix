using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="ISortable{TSortField}"/> implementations.
/// </summary>
/// <typeparam name="TSortField">The closed set of sortable fields for the list.</typeparam>
public sealed class SortableRequestValidator<TSortField> : AbstractValidator<ISortable<TSortField>>
    where TSortField : struct, Enum
{
    public SortableRequestValidator()
    {
        RuleFor(request => request.SortBy)
            .IsInEnum()
            .When(request => request.SortBy.HasValue);

        RuleFor(request => request.Direction)
            .IsInEnum()
            .When(request => request.Direction.HasValue);
    }
}
