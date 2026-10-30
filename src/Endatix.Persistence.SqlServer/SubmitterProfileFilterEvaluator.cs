using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer;

// A binary collation keeps case-sensitive matching, as PostgreSQL's @> has, whatever the database collation.
// SQL Server still ignores trailing spaces when it compares strings.
internal sealed class SubmitterProfileFilterEvaluator : IEvaluator
{
    private const string ExactCollation = "Latin1_General_100_BIN2";

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
            submissionQuery = Apply(submissionQuery, filter);
        }

        return (IQueryable<T>)submissionQuery;
    }

    private static IQueryable<Submission> Apply(IQueryable<Submission> query, FilterCriterion filter)
    {
        var path = SqlServerJsonObjectKeyFilter.BuildJsonValuePath(
            SubmissionFilterFields.GetSubmitterProfileFieldName(filter));
        var values = filter.Values.ToList();

        return filter.Operator switch
        {
            ExpressionType.Equal => query.Where(HasAnyOf(path, values)),
            ExpressionType.NotEqual => query.Where(IsMissingOrNoneOf(path, values)),
            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for submitter profile filters.")
        };
    }

    private static Expression<Func<Submission, bool>> HasAnyOf(string path, List<string> values)
    {
        IEnumerable<string?> candidates = values;
        return s => s.SubmitterProfileSnapshot != null &&
                    candidates.Contains(EF.Functions.Collate(
                        SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path),
                        ExactCollation));
    }

    private static Expression<Func<Submission, bool>> IsMissingOrNoneOf(string path, List<string> values)
    {
        IEnumerable<string?> candidates = values;
        return s => s.SubmitterProfileSnapshot == null ||
                    SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path) == null ||
                    !candidates.Contains(EF.Functions.Collate(
                        SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path),
                        ExactCollation));
    }
}
