using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.SearchCopy;

namespace Endatix.IntegrationTests;

/// <summary>
/// A form and its answers for the submissions-grid performance run: how many questions, how many are answered, and
/// the planted values the filters look for, in early and deep questions.
/// </summary>
/// <remarks>
/// Every <c>100</c>th row has "zebra", every <c>20</c>th "kiwi", every <c>4</c>th "mango", every <c>50</c>th a long
/// comment ending in "LongNeedle", and each row a date in 2025 or 2026. The early questions get automatic search
/// keys; the deep ones are added as people's keys, as a form editor would add them.
/// </remarks>
internal sealed record GridPerformanceShape(string Name, string Description, int Questions, double Answered, bool ShortKeys)
{
    public static GridPerformanceShape FromName(string name) => name switch
    {
        "worst" => new("worst", "1,000 questions, all answered", 1000, 1.0, ShortKeys: true),
        "mid" => new("mid", "200 questions, 50% answered", 200, 0.5, ShortKeys: false),
        _ => new("realistic", "1,000 questions, 20% answered, 2% long comments", 1000, 0.2, ShortKeys: false),
    };

    public const int EarlyZebra = 3;
    public const int EarlyKiwi = 4;
    public const int EarlyMango = 5;
    public const int Date = 7;
    public const int LongComment = 20;
    public int DeepZebra => Questions - 5;
    public int DeepKiwi => Questions / 2;
    public int DeepMango => Questions - 10;

    public string Key(int question) => ShortKeys ? $"q{question:D4}" : $"question{question}";

    /// <summary>Text questions, with a date question and a comment where the planted values go.</summary>
    public string Definition()
    {
        JsonArray elements = [];
        for (var question = 1; question <= Questions; question++)
        {
            JsonObject element = new() { ["type"] = question == LongComment ? "comment" : "text", ["name"] = Key(question) };
            if (question == Date)
            {
                element["inputType"] = "date";
            }

            elements.Add(element);
        }

        return new JsonObject { ["pages"] = new JsonArray(new JsonObject { ["elements"] = elements }) }.ToJsonString();
    }

    /// <summary>The questions people add after the automatic keys: the deep planted ones first, up to the cap.</summary>
    public IEnumerable<string> UserKeys(IReadOnlyCollection<string> automatic) =>
        new[] { DeepZebra, DeepKiwi, DeepMango }
            .Concat(Enumerable.Range(1, Questions))
            .Select(Key)
            .Where(key => !automatic.Contains(key))
            .Distinct()
            .Take(FormSearchKeys.MaxKeys - automatic.Count);

    public GridPerformanceRow Row(int index, FormSearchKeys searchKeys, GridPerformanceCounts counts)
    {
        GridPerformanceRowValues values = new(index, this);
        counts.Count(values);
        var dataJson = DataJson(values);
        return new GridPerformanceRow(dataJson, SearchJson(dataJson, searchKeys));
    }

    // Unanswered questions are written as null, as the flattener writes every schema key. The generated values hold
    // no character JSON escapes, so they are quoted as they are.
    private string DataJson(GridPerformanceRowValues values)
    {
        StringBuilder data = new(Questions * 24);
        data.Append('{');
        for (var question = 1; question <= Questions; question++)
        {
            data.Append(question > 1 ? "," : string.Empty).Append('"').Append(Key(question)).Append("\":");
            var value = values.Of(question);
            data.Append(value is null ? "null" : $"\"{value}\"");
        }

        return data.Append('}').ToString();
    }

    // The search copy is built by the flatten's own code, from the stored answers.
    private static string SearchJson(string dataJson, FormSearchKeys searchKeys)
    {
        using var document = JsonDocument.Parse(dataJson);
        var flattened = searchKeys.Keys.ToDictionary(
            key => key.Key,
            key => document.RootElement.TryGetProperty(key.Key, out var value) ? value.Clone() : (JsonElement?)null,
            StringComparer.Ordinal);
        return SearchCopyDocument.Build(flattened, searchKeys);
    }
}

internal sealed record GridPerformanceRow(string DataJson, string SearchJson);

internal sealed class GridPerformanceRowValues(int index, GridPerformanceShape shape)
{
    private static readonly string[] Vocabulary = BuildVocabulary();

    public bool Zebra => index % 100 == 0;
    public bool Kiwi => index % 20 == 0;
    public bool Mango => index % 4 == 0;
    public bool HasLongComment => index % 50 == 0;
    public string Date => new DateTime(2025, 1, 1).AddDays(index % 730).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string? Of(int question) => question switch
    {
        _ when question == GridPerformanceShape.Date => Date,
        _ when question == GridPerformanceShape.EarlyZebra || question == shape.DeepZebra => Zebra ? "A Zebra crossing " + Word(question) : Words(question),
        _ when question == GridPerformanceShape.EarlyKiwi || question == shape.DeepKiwi => Kiwi ? "Kiwi " + Word(question) : Words(question),
        _ when question == GridPerformanceShape.EarlyMango || question == shape.DeepMango => Mango ? "mango " + Word(question) : Words(question),
        _ when question == GridPerformanceShape.LongComment && HasLongComment => LongComment(question),
        _ => IsAnswered(question) ? Word(question) + " " + Word(question + 3) : null,
    };

    private bool IsAnswered(int question) =>
        shape.Answered >= 1.0 || ((uint)(index * 2654435761u ^ (uint)question * 40503u) % 1000) / 1000.0 < shape.Answered;

    private string LongComment(int question)
    {
        StringBuilder text = new();
        var length = 2000 + (index % 7) * 1000;
        for (var word = 0; text.Length < length; word++)
        {
            text.Append(Word(question + word)).Append(' ');
        }

        return text.Append("LongNeedle").ToString();
    }

    private string Words(int question) => Word(question) + " " + Word(question + 1);

    private string Word(int salt) => Vocabulary[(int)((uint)(index * 31 + salt * 17) % Vocabulary.Length)];

    // Pseudo-words from syllables that cannot spell the planted terms.
    private static string[] BuildVocabulary()
    {
        string[] syllables = ["ba", "do", "li", "mu", "ne", "po", "ra", "su", "ti", "vo", "ka", "he", "lo", "pi", "te", "ru"];
        Random random = new(7);
        HashSet<string> words = [];
        while (words.Count < 4000)
        {
            var word = string.Concat(Enumerable.Range(0, random.Next(2, 5)).Select(_ => syllables[random.Next(syllables.Length)]));
            if (!word.Contains("kiwi", StringComparison.Ordinal) && !word.Contains("mango", StringComparison.Ordinal) &&
                !word.Contains("zebra", StringComparison.Ordinal) && !word.Contains("needle", StringComparison.Ordinal))
            {
                words.Add(word);
            }
        }

        return [.. words];
    }
}

internal sealed class GridPerformanceCounts
{
    public int Rows { get; private set; }
    public int Zebra { get; private set; }
    public int Mango { get; private set; }
    public int MangoAndKiwi { get; private set; }
    public int AllThree { get; private set; }
    public int AllThreeIn2025 { get; private set; }
    public int LongComments { get; private set; }

    public void Count(GridPerformanceRowValues values)
    {
        Rows++;
        Zebra += values.Zebra ? 1 : 0;
        Mango += values.Mango ? 1 : 0;
        MangoAndKiwi += values.Mango && values.Kiwi ? 1 : 0;
        LongComments += values.HasLongComment ? 1 : 0;
        if (values.Zebra && values.Kiwi && values.Mango)
        {
            AllThree++;
            AllThreeIn2025 += string.CompareOrdinal(values.Date, "2026-01-01") < 0 ? 1 : 0;
        }
    }

    public override string ToString() =>
        $"rows {Rows}, zebra {Zebra}, mango {Mango}, mango+kiwi {MangoAndKiwi}, all three {AllThree} ({AllThreeIn2025} in 2025), long comments {LongComments}";
}
