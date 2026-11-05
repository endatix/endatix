using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// One release only (removal: endatix/endatix#1559): reading webhook settings, a stored retired key counts
/// as the key that replaced it when that one is absent, in any case, as the settings migration matches it. This keeps
/// delivering settings an earlier instance or an older Hub wrote after the migration ran. Writes with a retired key
/// are still refused (<see cref="WebHookConfiguration.RetiredKeyError(string?)"/>).
/// </summary>
internal static class LegacyWebHookSettingsKeys
{
    /// <summary>The configuration stored under <paramref name="key"/>, or under a retired key that it replaced.</summary>
    public static bool TryGetEventConfig(WebHookConfiguration config, string key, out WebHookEventConfig? eventConfig)
    {
        if (config.Events.TryGetValue(key, out eventConfig))
        {
            return true;
        }

        var accepted = WebHookConfiguration.RetiredEventKeys
            .Where(pair => string.Equals(pair.Value, key, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .Prepend(key)
            .ToArray();
        var stored = accepted
            .SelectMany(name => config.Events.Where(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Value)
            .FirstOrDefault();
        eventConfig = stored;
        return stored is not null;
    }
}
