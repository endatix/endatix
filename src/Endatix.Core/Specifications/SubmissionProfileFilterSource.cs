using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public interface ISubmissionProfileFilterSource
{
    IReadOnlyList<FilterCriterion> SubmitterProfileFilters { get; }
}

public static class SubmissionFilterFields
{
    public const string SubmitterProfilePrefix = "submitterProfile.";

    public static bool IsSubmitterProfileField(string field) =>
        field.StartsWith(SubmitterProfilePrefix, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<FilterCriterion> SelectSubmitterProfileFilters(IEnumerable<FilterCriterion> criteria) =>
        criteria
            .Where(criterion => IsSubmitterProfileField(criterion.Field))
            .ToList();

    public static string GetSubmitterProfileFieldName(FilterCriterion filter) =>
        filter.Field[SubmitterProfilePrefix.Length..];
}

public abstract class SubmissionsByFormIdFilteredSpecBase : Specification<Submission>, ISubmissionProfileFilterSource
{
    public IReadOnlyList<FilterCriterion> SubmitterProfileFilters { get; }

    protected SubmissionsByFormIdFilteredSpecBase(FilterParameters filterParams)
    {
        SubmitterProfileFilters = SubmissionFilterFields.SelectSubmitterProfileFilters(filterParams.Criteria);
    }
}

public abstract class SubmissionsByFormIdFilteredSpecBase<TResult> : Specification<Submission, TResult>, ISubmissionProfileFilterSource
{
    public IReadOnlyList<FilterCriterion> SubmitterProfileFilters { get; }

    protected SubmissionsByFormIdFilteredSpecBase(FilterParameters filterParams)
    {
        SubmitterProfileFilters = SubmissionFilterFields.SelectSubmitterProfileFilters(filterParams.Criteria);
    }
}
