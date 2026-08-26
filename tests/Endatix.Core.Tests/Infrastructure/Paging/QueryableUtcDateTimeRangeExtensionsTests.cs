using System.Linq.Expressions;
using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Tests.Infrastructure.Paging;

/// <summary>
/// Covers the <see cref="IQueryable{T}"/> overloads used by the Infrastructure/Reporting repositories
/// (the Ardalis specification overloads are covered by <c>WhereUtcRangeTests</c>).
/// </summary>
public sealed class QueryableUtcDateTimeRangeExtensionsTests
{
    private sealed class Row
    {
        public DateTime CreatedAt { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public DateTimeOffset? LastLoginAt { get; init; }
    }

    private static readonly DateTime Day2 = new(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NonNullable_AppliesInclusiveFromAndExclusiveTo()
    {
        var rows = new[]
        {
            new Row { CreatedAt = Day2.AddSeconds(-1) },
            new Row { CreatedAt = Day2 },
            new Row { CreatedAt = Day3.AddSeconds(-1) },
            new Row { CreatedAt = Day3 },
        }.AsQueryable();

        var result = rows.WhereUtcRange(x => x.CreatedAt, new UtcDateTimeRange(Day2, Day3)).ToList();

        result.Select(x => x.CreatedAt).Should().Equal(Day2, Day3.AddSeconds(-1));
    }

    [Fact]
    public void EmptyRange_LeavesQueryUntouched()
    {
        var rows = new[] { new Row { CreatedAt = Day2 } }.AsQueryable();

        rows.WhereUtcRange(x => x.CreatedAt, default).Should().BeSameAs(rows);
    }

    [Fact]
    public void Nullable_ExcludesNullsWhenBounded()
    {
        var rows = new[]
        {
            new Row { ModifiedAt = null },
            new Row { ModifiedAt = Day2 },
            new Row { ModifiedAt = Day2.AddDays(-1) },
        }.AsQueryable();

        var result = rows.WhereUtcRange(x => x.ModifiedAt, new UtcDateTimeRange(Day2, null)).ToList();

        result.Should().ContainSingle().Which.ModifiedAt.Should().Be(Day2);
    }

    [Fact]
    public void NullableDateTimeOffset_ComparesBoundAsUtc()
    {
        var rows = new[]
        {
            new Row { LastLoginAt = null },
            new Row { LastLoginAt = new DateTimeOffset(Day2, TimeSpan.Zero) },
            // Same instant as Day2, expressed in a +02:00 offset - must still match an inclusive From.
            new Row { LastLoginAt = new DateTimeOffset(
                DateTime.SpecifyKind(Day2.AddHours(2), DateTimeKind.Unspecified),
                TimeSpan.FromHours(2)) },
            new Row { LastLoginAt = new DateTimeOffset(Day2.AddSeconds(-1), TimeSpan.Zero) },
        }.AsQueryable();

        var result = rows.WhereUtcRange(x => x.LastLoginAt, new UtcDateTimeRange(Day2, null)).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void MaxValueExclusiveTo_IsComparedInclusively()
    {
        var rows = new[] { new Row { CreatedAt = DateTime.MaxValue } }.AsQueryable();

        var result = rows.WhereUtcRange(x => x.CreatedAt, new UtcDateTimeRange(null, DateTime.MaxValue));

        result.Should().ContainSingle();
    }

    /// <summary>
    /// EF Core renders <see cref="ConstantExpression"/> as a SQL literal and only parameterizes
    /// closure-style member access. Inlining the bound as a constant would emit a distinct SQL string
    /// per requested date and churn the server-side plan cache, so guard the tree shape here.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundedPredicates))]
    public void BoundIsLiftedToAParameterNotAConstant(Expression predicate)
    {
        var constants = new ConstantCollector();
        constants.Visit(predicate);

        constants.InlinedBoundTypes.Should().BeEmpty(
            "date bounds must reach EF as parameters, not inlined literals");
    }

    public static TheoryData<Expression> BoundedPredicates() => new()
    {
        UtcDateTimeRangeExpressions.CompareDateTime<Row>(
            x => x.CreatedAt, ExpressionType.GreaterThanOrEqual, Day2),
        UtcDateTimeRangeExpressions.CompareNullableDateTime<Row>(
            x => x.ModifiedAt, ExpressionType.LessThan, Day3),
        UtcDateTimeRangeExpressions.CompareNullableDateTimeOffset<Row>(
            x => x.LastLoginAt, ExpressionType.LessThan, Day3),
    };

    private sealed class ConstantCollector : ExpressionVisitor
    {
        public List<Type> InlinedBoundTypes { get; } = [];

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(DateTime) || node.Type == typeof(DateTimeOffset))
            {
                InlinedBoundTypes.Add(node.Type);
            }

            return base.VisitConstant(node);
        }
    }
}
