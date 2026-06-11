using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Persistence.PostgreSql.Builders;
using Endatix.Persistence.SqlServer.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Persistence;

public class SubmitterProfileFilterEvaluatorTests
{
    [Fact]
    public void PostgreSqlEvaluator_WithSubmitterProfileFilter_AppliesJsonContainsPredicate()
    {
        using ServiceProvider serviceProvider = CreatePostgreSqlProvider();
        IEvaluator evaluator = serviceProvider.GetRequiredService<IEvaluator>();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterProfile.email:test@example.com");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Expression.ToString().Should().Contain("JsonContains");
        result.Expression.ToString().Should().Contain(nameof(Submission.SubmitterProfileSnapshot));
    }

    [Fact]
    public void PostgreSqlEvaluator_WithoutSubmitterProfileFilter_ReturnsOriginalQuery()
    {
        using ServiceProvider serviceProvider = CreatePostgreSqlProvider();
        IEvaluator evaluator = serviceProvider.GetRequiredService<IEvaluator>();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterDisplayId:panelist-1");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Should().BeSameAs(query);
    }

    [Fact]
    public void SqlServerEvaluator_WithSubmitterProfileFilter_ThrowsNotSupportedException()
    {
        using ServiceProvider serviceProvider = CreateSqlServerProvider();
        IEvaluator evaluator = serviceProvider.GetRequiredService<IEvaluator>();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterProfile.email:test@example.com");

        Action act = () => _ = evaluator.GetQuery(query, specification);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*submitterProfile filters*PostgreSQL*");
    }

    [Fact]
    public void SqlServerEvaluator_WithoutSubmitterProfileFilter_ReturnsOriginalQuery()
    {
        using ServiceProvider serviceProvider = CreateSqlServerProvider();
        IEvaluator evaluator = serviceProvider.GetRequiredService<IEvaluator>();
        IQueryable<Submission> query = Enumerable.Empty<Submission>().AsQueryable();
        SubmissionsByFormIdCountSpec specification = CreateCountSpec("submitterDisplayId:panelist-1");

        IQueryable<Submission> result = evaluator.GetQuery(query, specification);

        result.Should().BeSameAs(query);
    }

    private static SubmissionsByFormIdCountSpec CreateCountSpec(string filterExpression) =>
        new(1, new FilterParameters([filterExpression]));

    private static ServiceProvider CreatePostgreSqlProvider()
    {
        ServiceCollection services = new();
        new PostgreSqlPersistenceBuilder(services).AddDbSpecificRepositories();

        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateSqlServerProvider()
    {
        ServiceCollection services = new();
        new SqlServerPersistenceBuilder(services).AddDbSpecificRepositories();

        return services.BuildServiceProvider();
    }
}
