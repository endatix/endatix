using Endatix.Core.Specifications.Parameters;
using Endatix.Core.Specifications.Common;

namespace Endatix.Core.Tests.Specifications.Common;

public class SpecificationHelperTests
{
    public enum TestEnum { Value1, Value2 }

    public class TestEntity
    {
        public int IntProperty { get; set; }
        public string? StringProperty { get; set; }
        public TestEnum EnumProperty { get; set; }
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

    [Fact]
    public void BuildFilterExpression_StartsWithOperator_ReturnsValidExpression()
    {
        // Arrange
        var field = nameof(TestEntity.StringProperty);
        var filter = new FilterCriterion($"{field}^:test");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity = new TestEntity { StringProperty = "test123" };
        result.Compile()(testEntity).Should().BeTrue();
        testEntity = new TestEntity { StringProperty = "123test" };
        result.Compile()(testEntity).Should().BeFalse();
    }

    [Fact]
    public void BuildFilterExpression_StartsWithOperator_CaseInsensitive()
    {
        // Arrange
        var field = nameof(TestEntity.StringProperty);
        var filter = new FilterCriterion($"{field}^:test");

        // Act
        var result = SpecificationHelper.BuildFilterExpression<TestEntity>(filter);

        // Assert
        result.Should().NotBeNull();
        var testEntity = new TestEntity { StringProperty = "TEST123" };
        result.Compile()(testEntity).Should().BeTrue();
    }

    [Fact]
    public void BuildFilterExpression_StartsWithOperator_NonStringProperty_ThrowsNotSupported()
    {
        // Arrange
        var field = nameof(TestEntity.IntProperty);
        var filter = new FilterCriterion($"{field}^:42");

        // Act & Assert
        var act = () => SpecificationHelper.BuildFilterExpression<TestEntity>(filter);
        act.Should().Throw<NotSupportedException>()
           .WithMessage("StartsWith operation is only supported for string properties.");
    }
}
