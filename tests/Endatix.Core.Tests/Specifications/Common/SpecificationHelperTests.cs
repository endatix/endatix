using Endatix.Core.Specifications.Parameters;
using Endatix.Core.Specifications.Common;
using System.Linq.Expressions;

namespace Endatix.Core.Tests.Specifications.Common;

public class SpecificationHelperTests
{
    public enum TestEnum { Value1, Value2 }

    public class TestEntity
    {
        public int IntProperty { get; set; }
        public string? StringProperty { get; set; }
        public TestEnum EnumProperty { get; set; }
        public DateTime DateTimeProperty { get; set; }
    }

    [Fact]
    public void BuildFilterExpression_NonExistentProperty_ThrowsArgumentException()
    {
        // Arrange
        var nonExistentField = "NonExistentProperty";
        var filter = new FilterCriterion($"{nonExistentField}:1");

        // Act
        var act = () => SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage(ErrorType.PropertyNotExist, "NonExistentProperty", "Field"));
    }

    [Fact]
    public void BuildFilterExpression_InvalidValueType_ThrowsArgumentException()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var filter = new FilterCriterion($"{field}:not-a-number");

        // Act
        var act = () => SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildFilterExpression_EqualOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var filter = new FilterCriterion($"{field}:42");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity = new TestEntity { IntProperty = 42 };
        result.Compile()(testEntity).Should().BeTrue();
    }

    [Fact]
    public void BuildFilterExpression_MultipleEqualValues_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var values = new[] { "1", "2" };
        var filter = new FilterCriterion($"{field}:1,2");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 1 };
        var testEntity2 = new TestEntity { IntProperty = 2 };
        var testEntity3 = new TestEntity { IntProperty = 3 };
        result.Compile()(testEntity1).Should().BeTrue();
        result.Compile()(testEntity2).Should().BeTrue();
        result.Compile()(testEntity3).Should().BeFalse();
    }

    [Fact]
    public void BuildFilterExpression_EnumValue_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.EnumProperty);
        var value = TestEnum.Value1.ToString();
        var filter = new FilterCriterion($"{field}:Value1");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity = new TestEntity { EnumProperty = TestEnum.Value1 };
        result.Compile()(testEntity).Should().BeTrue();
    }

    [Fact]
    public void BuildFilterExpression_NotEqualOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var filter = new FilterCriterion($"{field}!:42");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 42 };
        var testEntity2 = new TestEntity { IntProperty = 43 };
        result.Compile()(testEntity1).Should().BeFalse();
        result.Compile()(testEntity2).Should().BeTrue();
    }

    [Fact]
    public void BuildFilterExpression_NotEqualMultipleValues_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var filter = new FilterCriterion($"{field}!:1,2");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 1 };
        var testEntity2 = new TestEntity { IntProperty = 2 };
        var testEntity3 = new TestEntity { IntProperty = 3 };
        result.Compile()(testEntity1).Should().BeFalse();
        result.Compile()(testEntity2).Should().BeFalse();
        result.Compile()(testEntity3).Should().BeTrue();
    }

    [Fact]
    public void BuildFilterExpression_GreaterThanOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var value = "42";
        var filter = new FilterCriterion($"{field}>{value}");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 43 };
        var testEntity2 = new TestEntity { IntProperty = 41 };
        result.Compile()(testEntity1).Should().BeTrue();
        result.Compile()(testEntity2).Should().BeFalse();
    }

    [Fact]
    public void BuildFilterExpression_LessThanOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var value = "42";
        var filter = new FilterCriterion($"{field}<{value}");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 41 };
        var testEntity2 = new TestEntity { IntProperty = 43 };
        result.Compile()(testEntity1).Should().BeTrue();
        result.Compile()(testEntity2).Should().BeFalse();
    }

    [Fact]
    public void BuildFilterExpression_GreaterThanOrEqualOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var value = "42";
        var filter = new FilterCriterion($"{field}>:{value}");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 42 };
        var testEntity2 = new TestEntity { IntProperty = 43 };
        var testEntity3 = new TestEntity { IntProperty = 41 };
        result.Compile()(testEntity1).Should().BeTrue();
        result.Compile()(testEntity2).Should().BeTrue();
        result.Compile()(testEntity3).Should().BeFalse();
    }

    [Fact]
    public void BuildFilterExpression_LessThanOrEqualOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var value = "42";
        var filter = new FilterCriterion($"{field}<:{value}");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity1 = new TestEntity { IntProperty = 42 };
        var testEntity2 = new TestEntity { IntProperty = 41 };
        var testEntity3 = new TestEntity { IntProperty = 43 };
        result.Compile()(testEntity1).Should().BeTrue();
        result.Compile()(testEntity2).Should().BeTrue();
        result.Compile()(testEntity3).Should().BeFalse();
    }

    [Theory]
    [InlineData("2024-03-20T10:00:00")]                   // Unspecified kind
    [InlineData("2024-03-20T10:00:00Z")]                  // UTC kind
    [InlineData("2024-03-20T10:00:00+00:00")]             // UTC with offset
    [InlineData("2024-03-20T12:00:00+02:00")]             // Non-UTC timezone
    public void BuildFilterExpression_DifferentDateTimeFormats_AlwaysConvertsToUtc(string inputDateStr)
    {
        // Arrange
        var field = nameof(TestEntity.DateTimeProperty);
        var filterStr = $"{field}:{inputDateStr}";
        var filter = new FilterCriterion(filterStr);
        var expectedUtcDate = new DateTime(2024, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var expression = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        var binaryExpression = (BinaryExpression)expression.Body;
        var constantExpression = (ConstantExpression)binaryExpression.Right;
        var dateTimeValue = (DateTime)constantExpression.Value!;

        dateTimeValue.Kind.Should().Be(DateTimeKind.Utc);
        dateTimeValue.Should().Be(expectedUtcDate);
    }
}
