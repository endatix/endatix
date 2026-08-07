using Endatix.Core.Common.Translations;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Data.Querying;

public class RelationalJsonObjectKeyFilterExpressionsTests
{
    private sealed class FakeItem
    {
        public string Value { get; set; } = string.Empty;
        public string LabelsJson { get; set; } = "{}";
    }

    [Fact]
    public void NpgsqlFilter_WhereKeyMatches_IncludesExtractAndILike()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        NpgsqlJsonObjectKeyFilter filter = new();

        IQueryable<FakeItem> filtered = filter.WhereKeyMatches(
            source,
            nameof(FakeItem.LabelsJson),
            SurveyJsTranslationKeys.DefaultKey,
            "apple");

        string expression = filtered.Expression.ToString();
        expression.Should().Contain(nameof(NpgsqlJsonDbFunctions.ExtractObjectKeyText));
        expression.Should().Contain(SurveyJsTranslationKeys.DefaultKey);
        expression.Should().Contain(nameof(FakeItem.LabelsJson));
        expression.Should().Contain("ILike");
        expression.Should().NotContain("e.Value");
    }

    [Fact]
    public void NpgsqlFilter_WhereKeyMatches_SupportsNonDefaultLocaleKey()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        NpgsqlJsonObjectKeyFilter filter = new();

        IQueryable<FakeItem> filtered = filter.WhereKeyMatches(
            source,
            nameof(FakeItem.LabelsJson),
            "es",
            "manzana");

        string expression = filtered.Expression.ToString();
        expression.Should().Contain("es");
        expression.Should().Contain("ILike");
        expression.Should().NotContain("e.Value");
    }

    [Fact]
    public void NpgsqlFilter_OrderByKeyThenBy_OrdersByExtractedDefaultThenValue()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        NpgsqlJsonObjectKeyFilter filter = new();

        IOrderedQueryable<FakeItem> ordered = filter.OrderByKeyThenBy(
            source,
            nameof(FakeItem.LabelsJson),
            SurveyJsTranslationKeys.DefaultKey,
            nameof(FakeItem.Value));

        string expression = ordered.Expression.ToString();
        expression.Should().Contain(nameof(NpgsqlJsonDbFunctions.ExtractObjectKeyText));
        expression.Should().Contain(SurveyJsTranslationKeys.DefaultKey);
        expression.Should().Contain(nameof(FakeItem.Value));
    }

    [Fact]
    public void SqlServerFilter_WhereKeyMatches_IncludesJsonValuePathAndLike()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        SqlServerJsonObjectKeyFilter filter = new();

        IQueryable<FakeItem> filtered = filter.WhereKeyMatches(
            source,
            nameof(FakeItem.LabelsJson),
            SurveyJsTranslationKeys.DefaultKey,
            "apple");

        string expression = filtered.Expression.ToString();
        expression.Should().Contain(nameof(SqlServerJsonDbFunctions.JsonValue));
        expression.Should().Contain(SqlServerJsonObjectKeyFilter.BuildJsonValuePath(SurveyJsTranslationKeys.DefaultKey));
        expression.Should().Contain(nameof(FakeItem.LabelsJson));
        expression.Should().Contain("Like");
        expression.Should().NotContain("e.Value");
    }

    [Fact]
    public void SqlServerFilter_WhereKeyMatches_QuotesHyphenatedCultureInJsonPath()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        SqlServerJsonObjectKeyFilter filter = new();

        IQueryable<FakeItem> filtered = filter.WhereKeyMatches(
            source,
            nameof(FakeItem.LabelsJson),
            "en-US",
            "apple");

        string expression = filtered.Expression.ToString();
        expression.Should().Contain(SqlServerJsonObjectKeyFilter.BuildJsonValuePath("en-US"));
        SqlServerJsonObjectKeyFilter.BuildJsonValuePath("en-US").Should().Be("$.\"en-US\"");
    }

    [Fact]
    public void SqlServerFilter_OrderByKeyThenBy_OrdersByJsonValueThenValue()
    {
        IQueryable<FakeItem> source = new List<FakeItem>().AsQueryable();
        SqlServerJsonObjectKeyFilter filter = new();

        IOrderedQueryable<FakeItem> ordered = filter.OrderByKeyThenBy(
            source,
            nameof(FakeItem.LabelsJson),
            SurveyJsTranslationKeys.DefaultKey,
            nameof(FakeItem.Value));

        string expression = ordered.Expression.ToString();
        expression.Should().Contain(nameof(SqlServerJsonDbFunctions.JsonValue));
        expression.Should().Contain(SqlServerJsonObjectKeyFilter.BuildJsonValuePath(SurveyJsTranslationKeys.DefaultKey));
        expression.Should().Contain(nameof(FakeItem.Value));
    }
}
