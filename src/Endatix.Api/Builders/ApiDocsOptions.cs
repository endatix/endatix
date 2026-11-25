namespace Endatix.Api.Builders;

/// <summary>
/// The API docs: the OpenAPI document, the Scalar docs page, and Swagger UI. Set from <c>Endatix:Api:Docs</c> in
/// appsettings.json, or in code through <see cref="ApiOptions.Docs"/>.
/// </summary>
public sealed class ApiDocsOptions
{
    internal const string PATH_SETTING = "Endatix:Api:Docs:Path";

    internal const string DEFAULT_PATH = "/api-docs";

    internal const string SWAGGER_PATH_SETTING = "Endatix:Api:SwaggerPath";

    private string? _path;

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs. When <c>false</c>, neither the document nor any
    /// docs page is served. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the API docs page, which shows Scalar. Must start with "/". Null means "/api-docs". An
    /// empty path or "/" also means "/api-docs", and the root then redirects there unless the host maps its own root.
    /// </summary>
    public string? Path
    {
        get => _path;
        set => SetPath(value, PATH_SETTING);
    }

    /// <summary>Gets or sets the Swagger UI settings.</summary>
    public SwaggerUiDocsOptions SwaggerUi { get; set; } = new();

    /// <summary>The setting that gave <see cref="Path"/>, so a warning about the path names the key the host set.</summary>
    internal string PathSetting { get; private set; } = PATH_SETTING;

    /// <summary>A new key and its legacy key both set to different values. The API docs log each one at startup.</summary>
    internal IReadOnlyList<ApiDocsSettingConflict> Conflicts { get; set; } = [];

    internal void SetPath(string? value, string setting)
    {
        _path = value;
        PathSetting = setting;
    }

    /// <summary>
    /// A copy for a host that adds the docs explicitly: the docs on, the path given (else this path and the setting it
    /// came from), and Swagger UI on when the host configures it. Only the conflicts the call does not override carry
    /// over, so no warning names a setting the call replaced.
    /// </summary>
    internal ApiDocsOptions ForExplicitDocs(string? path, bool turnSwaggerUiOn)
    {
        var copy = new ApiDocsOptions
        {
            SwaggerUi = new SwaggerUiDocsOptions { Enabled = turnSwaggerUiOn ? true : SwaggerUi.Enabled, Path = SwaggerUi.Path },
            Conflicts = path is null ? Conflicts.Where(conflict => conflict.Setting == PATH_SETTING).ToList() : [],
        };
        copy.SetPath(path ?? _path, path is null ? PathSetting : PATH_SETTING);
        return copy;
    }
}

/// <summary>Swagger UI, served beside the Scalar docs page.</summary>
public sealed class SwaggerUiDocsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to serve Swagger UI. Null means this release's default, which is on.
    /// From the next release, null means off.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>Gets or sets the path of Swagger UI. Must start with "/". Null means "/swagger".</summary>
    public string? Path { get; set; }
}

/// <summary>A docs setting and its legacy key, both set to different values. The new setting wins.</summary>
internal sealed record ApiDocsSettingConflict(string Setting, string Value, string LegacySetting, string LegacyValue);
