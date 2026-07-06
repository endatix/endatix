using System.Text.Json;
using Ardalis.GuardClauses;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// Canonical SurveyJS element <c>type</c> values used by the reporting export pipeline.
/// </summary>
internal sealed record SurveyJsElementType
{
    private SurveyJsElementType(string name, SurveyJsElementCategory category, SurveyJsFlattening flattening)
    {
        Guard.Against.NullOrWhiteSpace(name);

        Name = name;
        Category = category;
        Flattening = flattening;
    }

    public string Name { get; }

    public SurveyJsElementCategory Category { get; }

    /// <summary>
    /// Default flattening when no per-element JSON overrides apply (e.g. <c>imagepicker</c> multi-select).
    /// </summary>
    public SurveyJsFlattening Flattening { get; }

    public bool Matches(string? typeName) =>
        string.Equals(Name, typeName, StringComparison.OrdinalIgnoreCase);

    public static SurveyJsElementType? TryResolve(string? typeName) =>
        typeName is not null && ByName.TryGetValue(typeName, out SurveyJsElementType? elementType)
            ? elementType
            : null;

    /// <summary>
    /// Resolves flattening for an element, accounting for type-specific JSON properties.
    /// </summary>
    public static SurveyJsFlattening ResolveFlattening(string? typeName, JsonElement element)
    {
        if (ImagePicker.Matches(typeName))
        {
            return element.TryGetProperty("multiSelect", out JsonElement multiSelect) &&
                   multiSelect.ValueKind == JsonValueKind.True
                ? SurveyJsFlattening.CheckboxChoices
                : SurveyJsFlattening.Simple;
        }

        return TryResolve(typeName)?.Flattening ?? SurveyJsFlattening.Simple;
    }

    public static bool IsNonData(string? typeName) =>
        TryResolve(typeName) is { Category: SurveyJsElementCategory.NonData };

    public static bool IsContainer(string? typeName) =>
        TryResolve(typeName) is { Category: SurveyJsElementCategory.Container };

    public static bool IsDrivingChoiceType(string? typeName) =>
        TryResolve(typeName) is SurveyJsElementType elementType && elementType.CanDriveNestedLoops;

    private bool CanDriveNestedLoops =>
        ReferenceEquals(this, Checkbox) || ReferenceEquals(this, Radiogroup);

    // Non-data (#nonvalue in SurveyJS schema)
    public static readonly SurveyJsElementType Html = new("html", SurveyJsElementCategory.NonData, SurveyJsFlattening.None);
    public static readonly SurveyJsElementType Image = new("image", SurveyJsElementCategory.NonData, SurveyJsFlattening.None);
    public static readonly SurveyJsElementType Empty = new("empty", SurveyJsElementCategory.NonData, SurveyJsFlattening.None);

    // Containers
    public static readonly SurveyJsElementType Panel = new("panel", SurveyJsElementCategory.Container, SurveyJsFlattening.None);
    public static readonly SurveyJsElementType PanelDynamic = new("paneldynamic", SurveyJsElementCategory.Container, SurveyJsFlattening.PanelDynamic);

    // Scalar (#question, single discrete value)
    public static readonly SurveyJsElementType Text = new("text", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Comment = new("comment", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Boolean = new("boolean", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Number = new("number", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Rating = new("rating", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType SignaturePad = new("signaturepad", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Expression = new("expression", SurveyJsElementCategory.Scalar, SurveyJsFlattening.Simple);

    // File
    public static readonly SurveyJsElementType FileUpload = new("file", SurveyJsElementCategory.File, SurveyJsFlattening.File);

    // Base select
    public static readonly SurveyJsElementType Checkbox = new("checkbox", SurveyJsElementCategory.BaseSelect, SurveyJsFlattening.CheckboxChoices);
    public static readonly SurveyJsElementType Tagbox = new("tagbox", SurveyJsElementCategory.BaseSelect, SurveyJsFlattening.CheckboxChoices);
    public static readonly SurveyJsElementType Radiogroup = new("radiogroup", SurveyJsElementCategory.BaseSelect, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType Dropdown = new("dropdown", SurveyJsElementCategory.BaseSelect, SurveyJsFlattening.Simple);
    public static readonly SurveyJsElementType ImagePicker = new("imagepicker", SurveyJsElementCategory.BaseSelect, SurveyJsFlattening.Simple);

    // Ranking
    public static readonly SurveyJsElementType Ranking = new("ranking", SurveyJsElementCategory.Ranking, SurveyJsFlattening.Ranking);

    // Multiple text
    public static readonly SurveyJsElementType MultipleText = new("multipletext", SurveyJsElementCategory.MultipleText, SurveyJsFlattening.MultipleText);

    // Matrix
    public static readonly SurveyJsElementType Matrix = new("matrix", SurveyJsElementCategory.Matrix, SurveyJsFlattening.Matrix);
    public static readonly SurveyJsElementType MatrixDropdown = new("matrixdropdown", SurveyJsElementCategory.Matrix, SurveyJsFlattening.Matrix);
    public static readonly SurveyJsElementType MatrixDynamic = new("matrixdynamic", SurveyJsElementCategory.Matrix, SurveyJsFlattening.Matrix);

    public static readonly SurveyJsElementType[] AllTypes =
    [
        Html,
        Image,
        Empty,
        Panel,
        PanelDynamic,
        Text,
        Comment,
        Boolean,
        Number,
        Rating,
        SignaturePad,
        Expression,
        FileUpload,
        Checkbox,
        Tagbox,
        Radiogroup,
        Dropdown,
        ImagePicker,
        Ranking,
        MultipleText,
        Matrix,
        MatrixDropdown,
        MatrixDynamic,
    ];

    public static readonly SurveyJsElementType[] NonDataTypes = TypesInCategory(SurveyJsElementCategory.NonData);

    public static readonly SurveyJsElementType[] ContainerTypes = TypesInCategory(SurveyJsElementCategory.Container);

    public static readonly SurveyJsElementType[] ScalarTypes = TypesInCategory(SurveyJsElementCategory.Scalar);

    public static readonly SurveyJsElementType[] PrimitiveTypes = ScalarTypes;

    public static readonly SurveyJsElementType[] FileTypes = TypesInCategory(SurveyJsElementCategory.File);

    public static readonly SurveyJsElementType[] BaseSelectTypes = TypesInCategory(SurveyJsElementCategory.BaseSelect);

    public static readonly SurveyJsElementType[] MultipleTextTypes = TypesInCategory(SurveyJsElementCategory.MultipleText);

    public static readonly SurveyJsElementType[] MatrixTypes = TypesInCategory(SurveyJsElementCategory.Matrix);

    public static readonly SurveyJsElementType[] ComplexTypes =
    [
        Checkbox,
        Tagbox,
        Ranking,
        MultipleText,
        FileUpload,
        Matrix,
        MatrixDropdown,
        MatrixDynamic,
        PanelDynamic,
    ];

    public static readonly SurveyJsElementType[] DrivingChoiceTypes =
    [
        Checkbox,
        Radiogroup,
    ];

    public static readonly string[] AllTypeNames = AllTypes.Select(type => type.Name).ToArray();

    public static readonly HashSet<string> NonDataTypeNames = ToNameSet(NonDataTypes);

    public static readonly HashSet<string> ContainerTypeNames = ToNameSet(ContainerTypes);

    private static readonly Dictionary<string, SurveyJsElementType> ByName = AllTypes.ToDictionary(
        type => type.Name,
        StringComparer.OrdinalIgnoreCase);

    private static SurveyJsElementType[] TypesInCategory(SurveyJsElementCategory category) =>
        AllTypes.Where(type => type.Category == category).ToArray();

    private static HashSet<string> ToNameSet(IEnumerable<SurveyJsElementType> types) =>
        new(types.Select(type => type.Name), StringComparer.OrdinalIgnoreCase);
}
