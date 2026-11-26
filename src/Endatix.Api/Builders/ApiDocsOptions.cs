namespace Endatix.Api.Builders;

/// <summary>
/// The API docs: the OpenAPI document and the docs page that shows it. Set from <c>Endatix:Api:Docs</c> in
/// appsettings.json, or in code through <see cref="ApiOptions.Docs"/>.
/// </summary>
public sealed class ApiDocsOptions
{
    internal const string PATH_SETTING = "Endatix:Api:Docs:Path";

    internal const string UI_SETTING = "Endatix:Api:Docs:Ui";

    internal const string DEFAULT_PATH = "/api-docs";

    internal const string SWAGGER_PATH_SETTING = "Endatix:Api:SwaggerPath";

    /// <summary>The <c>Docs:Ui</c> values the settings accept, in any case.</summary>
    internal const string ACCEPTED_UI_VALUES = "Scalar, Swagger, None";

    private static readonly Dictionary<string, ApiDocsUi> _uiValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scalar"] = ApiDocsUi.Scalar,
        ["SwaggerUi"] = ApiDocsUi.Swagger,
        ["Swagger"] = ApiDocsUi.Swagger,
        ["None"] = ApiDocsUi.None,
    };

    private string? _path;

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs. When <c>false</c>, neither the document nor any
    /// docs page is served. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the API docs page. Must start with "/". Null means "/api-docs". An empty path or "/"
    /// also means "/api-docs", and the root then redirects there unless the host maps its own root.
    /// </summary>
    public string? Path
    {
        get => _path;
        set => SetPath(value, PATH_SETTING);
    }

    /// <summary>
    /// Gets or sets the UI the docs page shows. Null means Scalar, or Swagger UI when the host sets
    /// <see cref="ApiOptions.ConfigureSwaggerUi"/>.
    /// </summary>
    public ApiDocsUi? Ui { get; set; }

    /// <summary>The setting that gave <see cref="Path"/>, so a warning about the path names the key the host set.</summary>
    internal string PathSetting { get; private set; } = PATH_SETTING;

    /// <summary>A new key and its legacy key both set to different values. The API docs log each one at startup.</summary>
    internal IReadOnlyList<ApiDocsSettingConflict> Conflicts { get; set; } = [];

    /// <summary>A <c>Docs:Ui</c> value the settings did not recognize. The API docs log it at startup.</summary>
    internal string? UnknownUi { get; set; }

    internal void SetPath(string? value, string setting)
    {
        _path = value;
        PathSetting = setting;
    }

    /// <summary>
    /// Sets <see cref="Ui"/> from a <c>Docs:Ui</c> value. Empty means unset. An unknown value shows Scalar and is kept
    /// for the startup warning, so a typo never stops the host.
    /// </summary>
    internal void SetUiSetting(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (_uiValues.TryGetValue(value.Trim(), out var ui))
        {
            Ui = ui;
            return;
        }

        Ui = ApiDocsUi.Scalar;
        UnknownUi = value;
    }

    /// <summary>
    /// A copy for a host that adds the docs explicitly: the docs on, the path given (else this path and the setting it
    /// came from), and this UI. Only the conflicts the call does not override carry over, so no warning names a setting
    /// the call replaced.
    /// </summary>
    internal ApiDocsOptions ForExplicitDocs(string? path)
    {
        var copy = new ApiDocsOptions
        {
            Ui = Ui,
            UnknownUi = UnknownUi,
            Conflicts = path is null ? Conflicts.Where(conflict => conflict.Setting == PATH_SETTING).ToList() : [],
        };
        copy.SetPath(path ?? _path, path is null ? PathSetting : PATH_SETTING);
        return copy;
    }
}

/// <summary>The UI the API docs page shows over the OpenAPI document.</summary>
public enum ApiDocsUi
{
    /// <summary>Scalar, the default.</summary>
    Scalar,

    /// <summary>Swagger UI, as NSwag serves it.</summary>
    Swagger,

    /// <summary>No page: only the OpenAPI document is served.</summary>
    None,
}

/// <summary>A docs setting and its legacy key, both set to different values. The new setting wins.</summary>
internal sealed record ApiDocsSettingConflict(string Setting, string Value, string LegacySetting, string LegacyValue);
