namespace Endatix.Framework.Modules;

/// <summary>
/// Marks a module that is registered only when its feature flag is enabled in
/// <c>Endatix:FeatureFlags</c> at host configuration time.
/// </summary>
public interface IHasFeatureFlag
{
    /// <summary>
    /// Feature flag key from <see cref="FeatureFlags.FeatureFlags"/>.
    /// </summary>
    string FeatureFlag { get; }
}
