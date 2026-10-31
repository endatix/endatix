using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data.Querying;

namespace Endatix.Persistence.PostgreSql;

/// <summary>
/// Applies <c>submitterProfile.{field}</c> list filters through <see cref="IRelationalSubmitterProfileFilter"/>.
/// </summary>
internal sealed class SubmitterProfileFilterEvaluator(IRelationalSubmitterProfileFilter profileFilter) : IEvaluator
{
    public bool IsCriteriaEvaluator => true;

    public IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
    {
        if (typeof(T) != typeof(Submission) ||
            specification is not ISubmissionProfileFilterSource profileFilterSpecification ||
            profileFilterSpecification.SubmitterProfileFilters.Count is 0)
        {
            return query;
        }

        var submissionQuery = (IQueryable<Submission>)query;
        foreach (var filter in profileFilterSpecification.SubmitterProfileFilters)
        {
            submissionQuery = profileFilter.Where(submissionQuery, s => s.SubmitterProfileSnapshot, filter);
        }

        return (IQueryable<T>)submissionQuery;
    }
}
