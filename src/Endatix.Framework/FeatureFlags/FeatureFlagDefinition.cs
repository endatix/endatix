using Ardalis.GuardClauses;

namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// One entry in the <see cref="FeatureFlagCatalogue"/>: everything the evaluator needs to resolve a
/// flag, independent of which provider ends up answering it.
/// </summary>
/// <param name="Key">
/// The canonical kebab-case flag key, e.g. <c>reporting-module</c>.
/// </param>
/// <param name="ConfigKey">
/// The path under <c>Endatix:FeatureFlags</c> that seeds this flag from configuration. Deliberately
/// separate from <paramref name="Key"/>: self-hosted operators already set these paths, and renaming
/// one would silently revert a feature to its default on upgrade.
/// </param>
/// <param name="ValueType">The CLR type of the flag's value.</param>
/// <param name="DefaultValue">
/// The value used when nothing resolves the flag. For a boolean <see cref="FeatureFlagClass.Entitlement"/>
/// this is the locked state; for a numeric one it is the free-tier value, never zero.
/// </param>
/// <param name="Class">Which system is the record for this flag's value.</param>
/// <param name="Scope">Where the flag is evaluated.</param>
public sealed record FeatureFlagDefinition(
    string Key,
    string ConfigKey,
    Type ValueType,
    object DefaultValue,
    FeatureFlagClass Class,
    FeatureFlagScope Scope)
{
    /// <summary>
    /// The canonical kebab-case flag key.
    /// </summary>
    public string Key { get; } = Guard.Against.NullOrWhiteSpace(Key);

    /// <summary>
    /// The configuration path under <c>Endatix:FeatureFlags</c> that seeds this flag.
    /// </summary>
    public string ConfigKey { get; } = Guard.Against.NullOrWhiteSpace(ConfigKey);

    /// <summary>
    /// The CLR type of the flag's value.
    /// </summary>
    public Type ValueType { get; } = Guard.Against.Null(ValueType);

    /// <summary>
    /// The value used when nothing resolves the flag.
    /// </summary>
    public object DefaultValue { get; } = Guard.Against.Null(DefaultValue);

    /// <summary>
    /// Creates a boolean flag definition.
    /// </summary>
    public static FeatureFlagDefinition Boolean(
        string key,
        string configKey,
        bool defaultValue,
        FeatureFlagClass flagClass,
        FeatureFlagScope scope) =>
        new(key, configKey, typeof(bool), defaultValue, flagClass, scope);
}
