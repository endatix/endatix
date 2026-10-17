using System.Globalization;
using System.Text;
using Endatix.Modules.Reporting.Features.SearchCopy;

namespace Endatix.IntegrationTests;

/// <summary>One measured scenario: what it should match, what it matched, and how long each run took.</summary>
internal sealed record GridMeasurement(string Name, int Expected, int Total, IReadOnlyList<double> Milliseconds)
{
    public bool IsWrong => Total != Expected;

    public double Percentile(double fraction)
    {
        var sorted = Milliseconds.Order().ToList();
        return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(fraction * sorted.Count) - 1)];
    }
}

/// <summary>
/// The Markdown report of a performance run, written to <see cref="PerformanceRun.OutputDirectory"/> and to the test
/// output, so runs can be compared as the grid changes.
/// </summary>
internal sealed class GridPerformanceReport(string name)
{
    private readonly StringBuilder _text = new();
    private readonly Dictionary<string, string> _attachments = [];

    public List<string> Wrong { get; } = [];

    public void Header(GridPerformanceShape shape, FormSearchKeys searchKeys, string load)
    {
        var automatic = searchKeys.Keys.Count(key => key.Source == SearchKeySources.Auto);
        Line(FormattableString.Invariant($"## {name}: {shape.Description}, {PerformanceRun.Rows:N0} rows, {PerformanceRun.Iterations} runs per scenario after 2 warm-ups"));
        Line($"search keys: {searchKeys.Keys.Count} ({automatic} automatic, {searchKeys.Keys.Count - automatic} added as people's keys)");
        Line(load);
    }

    public void Line(string text) => _text.AppendLine(text);

    public void Table(string title)
    {
        Line(string.Empty);
        Line($"### {title}");
        Line(string.Empty);
        Line("| scenario | rows | p50 ms | p95 ms |");
        Line("| --- | --- | --- | --- |");
    }

    public void Row(GridMeasurement measurement)
    {
        var rows = measurement.IsWrong
            ? $"{measurement.Expected} **got {measurement.Total}**"
            : measurement.Total.ToString(CultureInfo.InvariantCulture);
        Line(string.Create(
            CultureInfo.InvariantCulture,
            $"| {measurement.Name} | {rows} | {measurement.Percentile(0.5):F0} | {measurement.Percentile(0.95):F0} |"));
        if (measurement.IsWrong)
        {
            Wrong.Add(measurement.Name);
        }

        Write();
    }

    public void Attach(string suffix, string text) => _attachments[suffix] = text;

    public void Write()
    {
        Directory.CreateDirectory(PerformanceRun.OutputDirectory);
        File.WriteAllText(Path.Combine(PerformanceRun.OutputDirectory, name + ".md"), _text.ToString());
        foreach (var (suffix, text) in _attachments)
        {
            File.WriteAllText(Path.Combine(PerformanceRun.OutputDirectory, $"{name}.{suffix}.txt"), text);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(_text.ToString());
    }
}
