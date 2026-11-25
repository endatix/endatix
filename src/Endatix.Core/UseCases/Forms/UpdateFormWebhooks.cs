using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Forms;

internal static class UpdateFormWebhooks
{
    internal static WebHookConfiguration? Parse(string? webHookSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(webHookSettingsJson))
        {
            return null;
        }

        var webHookConfig = System.Text.Json.JsonSerializer.Deserialize<WebHookConfiguration>(webHookSettingsJson);
        if (webHookConfig?.Events is null || webHookConfig.Events.Count == 0)
        {
            return null;
        }

        return webHookConfig;
    }
}
