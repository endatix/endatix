using System.Net.Http.Json;
using System.Text.Json;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflicting_panel_values_block_at_both_locations_and_keep_independent_findings(bool sibling)
    {
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        var jsonData = $$"""
            {"elements":[{"type":"checkbox","name":"driver","valuePropertyName":"picked","choices":["a"]},
              {"type":"paneldynamic","name":"first","valueName":"driver","templateElements":[{"type":"text","name":"x"}]},
              {"type":"paneldynamic","name":"second","valueName":"driver","templateElements":[{"type":"text","name":"y"}]}
              {{(sibling ? """,{"type":"unknown","name":"sibling"}""" : "")}}]}
            """;
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await editor.PutAsJsonAsync(form + "/draft", new { jsonData }, Cancellation);
        saved.EnsureSuccessStatusCode();
        editor.DefaultRequestHeaders.IfMatch.Clear();

        var report = await CheckAsync(editor, form);

        report.GetProperty("outcome").GetString().Should().Be("block");
        var findings = report.GetProperty("findings").EnumerateArray().ToArray();
        findings.Should().NotContain(f => f.GetProperty("code").GetString() == "column/duplicate-key");
        var conflicts = findings.Where(f => f.GetProperty("code").GetString() == "panel/duplicate-value-name").ToArray();
        conflicts.Select(f => f.GetProperty("path").GetProperty("element").GetString())
            .Should().BeEquivalentTo(["/elements/name:first", "/elements/name:second"]);
        conflicts.Should().OnlyContain(f => f.GetProperty("blocking").GetBoolean() && !f.GetProperty("requiresAck").GetBoolean());
        findings.Any(f => f.GetProperty("code").GetString() == "element/unknown-type").Should().Be(sibling);
        (await CheckAsync(editor, form)).GetRawText().Should().Be(report.GetRawText());
    }
}
