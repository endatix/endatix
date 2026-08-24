using System.Reflection;
using System.Text.RegularExpressions;
using Endatix.Framework.FeatureFlags;

namespace Endatix.Framework.Tests.FeatureFlags;

public class FeatureFlagCatalogueTests
{
    private static readonly Regex KebabCase = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    private static IReadOnlyList<string> DeclaredConstants() =>
        typeof(Endatix.Framework.FeatureFlags.FeatureFlags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

    [Fact]
    public void Definitions_EveryDeclaredConstant_ResolvesToADefinition()
    {
        // Arrange
        var constants = DeclaredConstants();

        // Act
        var unresolved = constants
            .Where(constant => FeatureFlagCatalogue.FindByConfigKey(constant) is null)
            .ToList();

        // Assert
        constants.Should().NotBeEmpty("the catalogue is meaningless if there are no flag constants");
        unresolved.Should().BeEmpty("every FeatureFlags constant must have a catalogue entry keyed by its config path");
    }

    [Fact]
    public void Definitions_EveryDefinition_HasADeclaredConstant()
    {
        // Arrange
        var constants = DeclaredConstants();

        // Act
        var orphans = FeatureFlagCatalogue.Definitions
            .Where(definition => !constants.Contains(definition.ConfigKey, StringComparer.Ordinal))
            .Select(definition => definition.Key)
            .ToList();

        // Assert
        orphans.Should().BeEmpty("a catalogue entry with no constant cannot be referenced from code");
    }

    [Fact]
    public void Definitions_Keys_AreUnique()
    {
        // Arrange
        var keys = FeatureFlagCatalogue.Definitions.Select(definition => definition.Key);

        // Act
        var duplicates = keys.GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert
        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void Definitions_ConfigKeys_AreUnique()
    {
        // Arrange
        var configKeys = FeatureFlagCatalogue.Definitions.Select(definition => definition.ConfigKey);

        // Act
        var duplicates = configKeys.GroupBy(configKey => configKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert
        duplicates.Should().BeEmpty("two flags sharing a config path would resolve to each other's value");
    }

    [Fact]
    public void Definitions_Keys_AreKebabCase()
    {
        // Arrange
        var keys = FeatureFlagCatalogue.Definitions.Select(definition => definition.Key);

        // Act
        var malformed = keys.Where(key => !KebabCase.IsMatch(key)).ToList();

        // Assert
        malformed.Should().BeEmpty("evaluator keys are kebab-case by convention");
    }

    [Fact]
    public void Definitions_BooleanEntitlements_DefaultToTheLockedState()
    {
        // Arrange
        var booleanEntitlements = FeatureFlagCatalogue.Definitions
            .Where(definition => definition.Class == FeatureFlagClass.Entitlement)
            .Where(definition => definition.ValueType == typeof(bool));

        // Act
        var unlocked = booleanEntitlements
            .Where(definition => (bool)definition.DefaultValue)
            .Select(definition => definition.Key)
            .ToList();

        // Assert
        unlocked.Should().BeEmpty("an entitlement must never resolve to enabled when nothing answers it");
    }

    [Fact]
    public void Definitions_DefaultValue_MatchesTheDeclaredValueType()
    {
        // Arrange
        var definitions = FeatureFlagCatalogue.Definitions;

        // Act
        var mismatched = definitions
            .Where(definition => !definition.ValueType.IsInstanceOfType(definition.DefaultValue))
            .Select(definition => definition.Key)
            .ToList();

        // Assert
        mismatched.Should().BeEmpty();
    }

    [Fact]
    public void FindByKey_KnownKey_ReturnsTheDefinition()
    {
        // Arrange
        var expected = FeatureFlagCatalogue.Definitions.First();

        // Act
        var found = FeatureFlagCatalogue.FindByKey(expected.Key);

        // Assert
        found.Should().BeSameAs(expected);
    }

    [Fact]
    public void FindByConfigKey_KnownConfigKey_ReturnsTheDefinition()
    {
        // Arrange
        var expected = FeatureFlagCatalogue.Definitions.First();

        // Act
        var found = FeatureFlagCatalogue.FindByConfigKey(expected.ConfigKey);

        // Assert
        found.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("no-such-flag")]
    [InlineData("")]
    [InlineData("   ")]
    public void FindByKey_UnknownOrBlankKey_ReturnsNull(string key)
    {
        // Arrange & Act
        var found = FeatureFlagCatalogue.FindByKey(key);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void FindByConfigKey_UnknownConfigKey_ReturnsNull()
    {
        // Arrange & Act
        var found = FeatureFlagCatalogue.FindByConfigKey("NoSuchFlag");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void Definitions_ReportingModule_IsDeploymentScoped()
    {
        // Arrange
        var reportingModule = FeatureFlagCatalogue.FindByConfigKey(
            Endatix.Framework.FeatureFlags.FeatureFlags.ReportingModule);

        // Act
        var scope = reportingModule?.Scope;

        // Assert
        scope.Should().Be(
            FeatureFlagScope.Deployment,
            "ShouldRegister reads it at startup, where no tenant or user exists");
    }
}
