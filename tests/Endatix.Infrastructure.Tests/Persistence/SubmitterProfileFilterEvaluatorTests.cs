using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using PostgreSqlEvaluator = Endatix.Persistence.PostgreSql.SubmitterProfileFilterEvaluator;
using SqlServerEvaluator = Endatix.Persistence.SqlServer.SubmitterProfileFilterEvaluator;

namespace Endatix.Infrastructure.Tests.Persistence;

public class SubmitterProfileFilterEvaluatorTests
{
    [Fact]
    public void PostgreSqlEvaluator_WithSubmitterProfileFilter_AppliesJsonContainsPredicate()
    {
        PostgreSqlEvaluator evaluator = new();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterProfile.email:test@example.com");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Expression.ToString().Should().Contain("JsonContains");
        result.Expression.ToString().Should().Contain(nameof(Submission.SubmitterProfileSnapshot));
    }

    [Fact]
    public void PostgreSqlEvaluator_WithoutSubmitterProfileFilter_ReturnsOriginalQuery()
    {
        PostgreSqlEvaluator evaluator = new();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterDisplayId:panelist-1");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Should().BeSameAs(query);
    }

    [Fact]
    public void SqlServerEvaluator_WithSubmitterProfileFilter_ThrowsNotSupportedException()
    {
        SqlServerEvaluator evaluator = new();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterProfile.email:test@example.com");

        Action act = () => _ = evaluator.GetQuery(query, specification);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*submitterProfile filters*PostgreSQL*");
    }

    [Fact]
    public void SqlServerEvaluator_WithoutSubmitterProfileFilter_ReturnsOriginalQuery()
    {
        SqlServerEvaluator evaluator = new();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterDisplayId:panelist-1");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Should().BeSameAs(query);
    }

    private static SubmissionsByFormIdCountSpec CreateCountSpec(string filterExpression) =>
        new(1, new FilterParameters([filterExpression]));
}
