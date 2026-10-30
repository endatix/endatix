using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// What Crunch checks when it imports a Shoji codebook with its CSV: every CSV header is a variable alias (or a
/// subvariable alias), every alias has a column, and every non-empty cell fits its variable's type. Crunch accepts an
/// empty cell as missing and rejects anything else that does not parse ("CSV numeric format error").
/// </summary>
internal static partial class CrunchImportCheck
{
    private static readonly string[] _datetimeFormats =
        ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "yyyy-MM-dd"];

    public static IReadOnlyList<string> Problems(string shojiJson, string csv) => Problems(shojiJson, csv, scoped: false);

    /// <summary>
    /// The checks for a CSV exported with a column scope: the codebook is not scoped, so a variable outside the scope
    /// has no column, but every header is still a variable and every cell still fits it.
    /// </summary>
    public static IReadOnlyList<string> ScopedProblems(string shojiJson, string csv) => Problems(shojiJson, csv, scoped: true);

    private static List<string> Problems(string shojiJson, string csv, bool scoped)
    {
        var variables = VariablesByAlias(shojiJson);
        var (header, rows) = Read(csv);
        List<string> problems = [];
        problems.AddRange(header.Where(alias => !variables.ContainsKey(alias)).Select(alias => $"header '{alias}' has no Shoji variable"));
        if (!scoped)
        {
            problems.AddRange(variables.Keys.Where(alias => !header.Contains(alias)).Select(alias => $"Shoji alias '{alias}' has no CSV column"));
        }

        problems.AddRange(CellProblems(new Columns(header, variables), rows));
        return problems;
    }

    /// <summary>The CSV header and the Shoji variable each alias names.</summary>
    private sealed record Columns(IReadOnlyList<string> Header, IReadOnlyDictionary<string, JsonElement> Variables);

    private static IEnumerable<string> CellProblems(Columns columns, IReadOnlyList<string[]> rows) =>
        rows.SelectMany((row, index) => RowProblems(columns, row, line: index + 2));

    private static IEnumerable<string> RowProblems(Columns columns, string[] row, int line)
    {
        for (var field = 0; field < columns.Header.Count; field++)
        {
            var alias = columns.Header[field];
            var value = field < row.Length ? row[field] : string.Empty;
            if (columns.Variables.TryGetValue(alias, out var variable) && !Fits(variable, value))
            {
                yield return $"line {line}, field {field + 1} ({alias}, {TypeOf(variable)}): '{value}'";
            }
        }
    }

    private static bool Fits(JsonElement variable, string value) =>
        value.Length == 0 ||
        TypeOf(variable) switch
        {
            "numeric" => Number().IsMatch(value),
            "datetime" => DateTime.TryParseExact(value, _datetimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _),
            "categorical" or "categorical_array" or "multiple_response" => IsCategoryId(variable, value),
            _ => true,
        };

    private static bool IsCategoryId(JsonElement variable, string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
        variable.GetProperty("categories").EnumerateArray().Any(category => category.GetProperty("id").GetInt32() == id);

    private static string TypeOf(JsonElement variable) => variable.GetProperty("type").GetString()!;

    private static Dictionary<string, JsonElement> VariablesByAlias(string shojiJson)
    {
        using var document = JsonDocument.Parse(shojiJson);
        var metadata = document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        Dictionary<string, JsonElement> variables = new(StringComparer.Ordinal);
        foreach (var variable in metadata.EnumerateObject().Select(entry => entry.Value.Clone()))
        {
            foreach (var alias in AliasesOf(variable))
            {
                variables[alias] = variable;
            }
        }

        return variables;
    }

    // An array variable is imported from one CSV column per subvariable.
    private static IEnumerable<string> AliasesOf(JsonElement variable) =>
        variable.TryGetProperty("subvariables", out var subvariables)
            ? subvariables.EnumerateArray().Select(subvariable => subvariable.GetProperty("alias").GetString()!)
            : [variable.GetProperty("alias").GetString()!];

    private static (List<string> Header, List<string[]> Rows) Read(string csv)
    {
        using var reader = new CsvReader(new StringReader(csv), CultureInfo.InvariantCulture);
        List<string[]> records = [];
        while (reader.Read())
        {
            records.Add(reader.Parser.Record ?? []);
        }

        return ([.. records[0]], records.Skip(1).ToList());
    }

    [GeneratedRegex(@"^-?\d+(\.\d+)?([eE][-+]?\d+)?$")]
    private static partial Regex Number();
}
