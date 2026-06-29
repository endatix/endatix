using Endatix.Api.Common;
using Endatix.Infrastructure.Data.Config;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Base class for submission request models with common properties.
/// </summary>
public abstract class BaseSubmissionRequest
{
    /// <summary>
    /// The ID of the form for which the submission is made.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Boolean flag to indicate if a submission is complete. Optional
    /// </summary>
    public bool? IsComplete { get; set; }

    /// <summary>
    /// Current page if the form has multiple pages. Optional
    /// </summary>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Stringified form submission data
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    /// Stringified metadata related to the form submission
    /// </summary>
    public string? Metadata { get; set; }
}

internal static class SubmissionRequestValidationExtensions
{
    internal static void ApplyBaseSubmissionRules<T>(this AbstractValidator<T> validator)
        where T : BaseSubmissionRequest
    {
        validator.RuleFor(x => x.FormId)
            .GreaterThan(0);

        validator.RuleFor(x => x.JsonData)
            .ValidJsonString()
            .When(x => x.JsonData != null);

        validator.RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);
    }
}
