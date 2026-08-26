using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public sealed class WhereUtcRangeTests
{
    [Fact]
    public void EmptyRange_MatchesAll()
    {
        var spec = new CustomQuestionSpecifications.ListFilter();
        var question = CreateQuestion(createdAt: new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        Matches(spec, question).Should().BeTrue();
    }

    [Fact]
    public void InclusiveFrom_IncludesBoundary()
    {
        var from = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var spec = new CustomQuestionSpecifications.ListFilter(
            created: new UtcDateTimeRange(from, null));

        Matches(spec, CreateQuestion(createdAt: from)).Should().BeTrue();
        Matches(spec, CreateQuestion(createdAt: from.AddSeconds(-1))).Should().BeFalse();
    }

    [Fact]
    public void ExclusiveTo_ExcludesBoundary()
    {
        var to = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var spec = new CustomQuestionSpecifications.ListFilter(
            created: new UtcDateTimeRange(null, to));

        Matches(spec, CreateQuestion(createdAt: to)).Should().BeFalse();
        Matches(spec, CreateQuestion(createdAt: to.AddSeconds(-1))).Should().BeTrue();
    }

    [Fact]
    public void MaxValueExclusiveTo_IsInclusive()
    {
        var spec = new CustomQuestionSpecifications.ListFilter(
            created: new UtcDateTimeRange(null, DateTime.MaxValue));

        Matches(spec, CreateQuestion(createdAt: DateTime.MaxValue)).Should().BeTrue();
    }

    [Fact]
    public void NullableModified_ExcludesNullWhenBounded()
    {
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var spec = new CustomQuestionSpecifications.ListFilter(
            modified: new UtcDateTimeRange(from, null));

        Matches(spec, CreateQuestion(modifiedAt: null)).Should().BeFalse();
        Matches(spec, CreateQuestion(modifiedAt: from)).Should().BeTrue();
        Matches(spec, CreateQuestion(modifiedAt: from.AddDays(-1))).Should().BeFalse();
    }

    private static bool Matches(ISpecification<CustomQuestion> spec, CustomQuestion question) =>
        spec.WhereExpressions.All(where => where.FilterFunc(question));

    private static CustomQuestion CreateQuestion(DateTime? createdAt = null, DateTime? modifiedAt = null)
    {
        var question = new CustomQuestion(
            SampleData.TENANT_ID,
            "rating",
            """{"type":"rating"}""",
            "Rating");

        if (createdAt.HasValue)
        {
            typeof(CustomQuestion)
                .GetProperty(nameof(CustomQuestion.CreatedAt))!
                .SetValue(question, createdAt.Value);
        }

        if (modifiedAt.HasValue)
        {
            typeof(CustomQuestion)
                .GetProperty(nameof(CustomQuestion.ModifiedAt))!
                .SetValue(question, modifiedAt.Value);
        }

        return question;
    }
}
