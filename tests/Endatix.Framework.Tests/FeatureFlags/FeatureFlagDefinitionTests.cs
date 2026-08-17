using Endatix.Framework.FeatureFlags;

namespace Endatix.Framework.Tests.FeatureFlags;

public class FeatureFlagDefinitionTests
{
    private static FeatureFlagDefinition Create(
        string key = "sample-flag",
        string configKey = "SampleFlag",
        Type? valueType = null,
        object? defaultValue = null,
        FeatureFlagClass flagClass = FeatureFlagClass.Rollout,
        FeatureFlagScope scope = FeatureFlagScope.Tenant) =>
        new(key, configKey, valueType ?? typeof(bool), defaultValue ?? false, flagClass, scope);

    [Fact]
    public void Constructor_ValidArguments_CreatesDefinition()
    {
        // Arrange & Act
        var definition = Create();

        // Assert
        definition.Key.Should().Be("sample-flag");
        definition.ConfigKey.Should().Be("SampleFlag");
        definition.ValueType.Should().Be<bool>();
        definition.DefaultValue.Should().Be(false);
        definition.Class.Should().Be(FeatureFlagClass.Rollout);
        definition.Scope.Should().Be(FeatureFlagScope.Tenant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankKey_Throws(string? key)
    {
        // Arrange & Act
        var act = () => Create(key: key!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankConfigKey_Throws(string? configKey)
    {
        // Arrange & Act
        var act = () => Create(configKey: configKey!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullValueType_Throws()
    {
        // Arrange & Act — constructed directly; the Create helper substitutes a default for null
        var act = () => new FeatureFlagDefinition(
            "sample-flag", "SampleFlag", null!, false, FeatureFlagClass.Rollout, FeatureFlagScope.Tenant);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullDefaultValue_Throws()
    {
        // Arrange & Act
        var act = () => new FeatureFlagDefinition(
            "sample-flag", "SampleFlag", typeof(bool), null!, FeatureFlagClass.Rollout, FeatureFlagScope.Tenant);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_DefaultValueDoesNotMatchValueType_Throws()
    {
        // Arrange & Act
        var act = () => Create(valueType: typeof(bool), defaultValue: "not-a-bool");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not match the declared value type*");
    }

    [Fact]
    public void Constructor_NumericDefaultMatchingItsValueType_IsAccepted()
    {
        // Arrange & Act
        var definition = Create(valueType: typeof(int), defaultValue: 100);

        // Assert
        definition.DefaultValue.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Constructor_UndefinedClass_Throws(int flagClass)
    {
        // Arrange & Act
        var act = () => Create(flagClass: (FeatureFlagClass)flagClass);

        // Assert
        act.Should().Throw<ArgumentException>(
            "zero must be rejected too — FeatureFlagClass has no zero member precisely so a definition cannot acquire a class by default");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Constructor_UndefinedScope_Throws(int scope)
    {
        // Arrange & Act
        var act = () => Create(scope: (FeatureFlagScope)scope);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Boolean_ValidArguments_CreatesBooleanDefinition()
    {
        // Arrange & Act
        var definition = FeatureFlagDefinition.Boolean(
            key: "sample-flag",
            configKey: "SampleFlag",
            defaultValue: true,
            flagClass: FeatureFlagClass.Entitlement,
            scope: FeatureFlagScope.Deployment);

        // Assert
        definition.ValueType.Should().Be<bool>();
        definition.DefaultValue.Should().Be(true);
        definition.Class.Should().Be(FeatureFlagClass.Entitlement);
        definition.Scope.Should().Be(FeatureFlagScope.Deployment);
    }
}
