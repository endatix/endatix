using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Tests.Infrastructure.Domain;

public sealed class ComparisonOperatorsTests
{
    [Fact]
    public void LessThan_NullLeft_IsTrueOnlyForNonNullRight()
    {
        ComparisonOperators.LessThan<string>(null, "a").Should().BeTrue();
        ComparisonOperators.LessThan<string>(null, null).Should().BeFalse();
    }

    [Fact]
    public void LessThan_NonNullLeft_UsesCompareTo()
    {
        ComparisonOperators.LessThan("a", "b").Should().BeTrue();
        ComparisonOperators.LessThan("b", "a").Should().BeFalse();
        ComparisonOperators.LessThan("a", null).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqual_NullLeft_IsAlwaysTrue()
    {
        ComparisonOperators.LessThanOrEqual<string>(null, null).Should().BeTrue();
        ComparisonOperators.LessThanOrEqual<string>(null, "a").Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_NonNullLeft_UsesCompareTo()
    {
        ComparisonOperators.LessThanOrEqual("a", "a").Should().BeTrue();
        ComparisonOperators.LessThanOrEqual("b", "a").Should().BeFalse();
        ComparisonOperators.LessThanOrEqual("a", null).Should().BeFalse();
    }
}
