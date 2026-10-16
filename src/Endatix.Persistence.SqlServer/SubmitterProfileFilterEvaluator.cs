using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer;

/// <summary>
/// Applies <c>submitterProfile.{field}</c> list filters with <c>JSON_VALUE</c> on the profile snapshot.
/// </summary>
/// <remarks>
/// Values compare under a binary collation, so a match is exact, as with PostgreSQL's <c>@&gt;</c>. A database
/// collation that ignores case would otherwise widen the filter on SQL Server only.
/// </remarks>
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
        var path = JsonPathOf(SubmissionFilterFields.GetSubmitterProfileFieldName(filter));
        var values = filter.Values.ToList();

        return filter.Operator switch
        {
            ExpressionType.Equal => query.Where(s =>
                s.SubmitterProfileSnapshot != null &&
                values.Contains(EF.Functions.Collate(
                    SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path)!,
                    ExactCollation))),
            ExpressionType.NotEqual => query.Where(s =>
                s.SubmitterProfileSnapshot == null ||
                SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path) == null ||
                !values.Contains(EF.Functions.Collate(
                    SqlServerJsonDbFunctions.JsonValue(s.SubmitterProfileSnapshot, path)!,
                    ExactCollation))),
            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for submitter profile filters.")
        };
    }

    // Quoted, so a field name with a dot or a space is one key, not a nested path.
    private static string JsonPathOf(string field) =>
        $"$.\"{field.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
