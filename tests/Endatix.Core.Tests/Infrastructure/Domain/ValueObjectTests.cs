using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Tests.Infrastructure.Domain;

public sealed class ValueObjectTests
{
    private sealed class Pair(object? first, object? second) : ValueObject
    {
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return first!;
            yield return second!;
        }
    }

    private sealed class Other(object? first, object? second) : ValueObject
    {
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return first!;
            yield return second!;
        }
    }

    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        // Arrange
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);

        // Act & Assert
        left.Equals(right).Should().BeTrue();
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_NullOrDifferentType_ReturnsFalse()
    {
        // Arrange
        var pair = new Pair("a", 1);
        ValueObject? missing = null;
        ValueObject? alsoMissing = null;

        // Act & Assert
        pair.Equals((object?)null).Should().BeFalse();
        (pair == missing).Should().BeFalse();
        (missing == pair).Should().BeFalse();
        (missing == alsoMissing).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        // Act
        var equal = new Pair("a", 1).Equals(new Other("a", 1));

        // Assert
        equal.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_NullComponent_DoesNotThrow()
    {
        // Act
        var hash = () => new Pair(null, 1).GetHashCode();

        // Assert
        hash.Should().NotThrow();
    }

    [Fact]
    public void CompareTo_Null_ReturnsOne()
    {
        new Pair("a", 1).CompareTo((object?)null).Should().Be(1);
        new Pair("a", 1).CompareTo((ValueObject?)null).Should().Be(1);
    }

    [Fact]
    public void CompareTo_DifferentTypes_OrdersByTypeName()
    {
        new Pair("a", 1).CompareTo(new Other("a", 1)).Should().NotBe(0);
    }

    [Fact]
    public void CompareTo_ComparesComponentsInOrder()
    {
        // Arrange
        var low = new Pair("a", 1);
        var high = new Pair("a", 2);

        // Act & Assert
        low.CompareTo(high).Should().BeNegative();
        high.CompareTo(low).Should().BePositive();
        low.CompareTo(new Pair("a", 1)).Should().Be(0);
    }

    [Fact]
    public void CompareTo_NullComponents_SortBeforeValues()
    {
        new Pair(null, null).CompareTo(new Pair(null, null)).Should().Be(0);
        new Pair(null, 1).CompareTo(new Pair("a", 1)).Should().BeNegative();
        new Pair("a", 1).CompareTo(new Pair(null, 1)).Should().BePositive();
    }

    [Fact]
    public void CompareTo_NonComparableComponents_UsesEquality()
    {
        // Arrange
        var marker = new object();

        // Act & Assert
        new Pair(marker, 1).CompareTo(new Pair(marker, 1)).Should().Be(0);
        new Pair(new object(), 1).CompareTo(new Pair(new object(), 1)).Should().Be(-1);
    }

    [Fact]
    public void RelationalOperators_OrderByCompareTo()
    {
        // Arrange
        var low = new Pair("a", 1);
        var high = new Pair("a", 2);

        // Act & Assert
        (low < high).Should().BeTrue();
        (low <= high).Should().BeTrue();
        (high > low).Should().BeTrue();
        (high >= low).Should().BeTrue();
        (high < low).Should().BeFalse();
        (low > high).Should().BeFalse();
    }

    [Fact]
    public void RelationalOperators_NullSortsFirst()
    {
        // Arrange
        Pair? none = null;
        Pair? alsoNone = null;
        var pair = new Pair("a", 1);

        // Act & Assert
        (none < pair).Should().BeTrue();
        (none <= pair).Should().BeTrue();
        (pair > none).Should().BeTrue();
        (pair < none).Should().BeFalse();
        (none < alsoNone).Should().BeFalse();
        (none <= alsoNone).Should().BeTrue();
    }

    [Fact]
    public void GetUnproxiedType_ProxyTypeName_ReturnsBaseType()
    {
        ValueObject.GetUnproxiedType(new Pair("a", 1)).Should().Be<Pair>();
    }
}
