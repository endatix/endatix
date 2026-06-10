using System.Linq.Expressions;
using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql;

internal sealed class SubmitterProfileFilterEvaluator : IEvaluator
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
            var profileField = SubmissionFilterFields.GetSubmitterProfileFieldName(filter);
            var payloads = filter.Values
                .Select(value => BuildJsonContainsPayload(profileField, value))
                .ToList();

            submissionQuery = filter.Operator switch
            {
                ExpressionType.Equal => submissionQuery.Where(s =>
                    s.SubmitterProfileSnapshot != null &&
                    payloads.Any(payload => EF.Functions.JsonContains(s.SubmitterProfileSnapshot, payload))),
                ExpressionType.NotEqual => submissionQuery.Where(s =>
                    s.SubmitterProfileSnapshot == null ||
                    payloads.All(payload => !EF.Functions.JsonContains(s.SubmitterProfileSnapshot, payload))),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for submitter profile filters.")
            };
        }

        return (IQueryable<T>)submissionQuery;
    }

    private static string BuildJsonContainsPayload(string profileField, string value) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { [profileField] = value });
}
