using System.Text.Json;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class GuardedLoopCheckTests
{
    [Theory]
    [InlineData(2, 0, 4)]
    [InlineData(1, 1, 3)]
    public void Check_FileBudget_CountsLoopFilesButNotTheDirectFile(int budget, int incomplete, int emitted)
    {
        var survey = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"file","name":"direct"},{"type":"radiogroup","name":"source","choices":["a","b"]},
              {"type":"paneldynamic","name":"loop","loopSource":["source"],"templateElements":[{"type":"file","name":"upload"}]}]}
            """);

        var result = FormDefinitionFlattener.Check(survey, new SchemaCompilationLimits { MaxColumns = budget });

        result.ColumnCount.Should().Be(emitted);
        result.Incomplete.Count.Should().Be(incomplete);
        result.Incomplete.Values.Count(limit => limit.LimitKind == SchemaCompilationLimitKind.MaxColumns).Should().Be(incomplete);
        var check = new ColumnKeyCheck(Options.Create(new ReportingOptions()));
        var findings = check.Inspect(survey, new SchemaCompilationLimits { MaxColumns = budget });
        findings.Count(f => f.Code == "column/check-incomplete" && f.Blocking && !f.RequiresAck
            && f.Path.Element == "/elements/name:loop/templateElements/name:upload").Should().Be(incomplete);
    }

    [Fact]
    public void Check_GuardedTemplate_EmitsItsPrefixOnlyOnceWhileSiblingFinishesBothIterations()
    {
        var survey = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"radiogroup","name":"source","choices":["a","b"]},
              {"type":"paneldynamic","name":"loop","loopSource":["source"],"templateElements":[
                {"type":"matrixdynamic","name":"bad","rowCount":1,"columns":["x","y","z"]},{"type":"text","name":"ok"}]},
              {"type":"text","name":"loop__b__ok"},{"type":"text","name":"loop__a__bad__0__x"}]}
            """);

        var result = FormDefinitionFlattener.Check(survey, new SchemaCompilationLimits { MaxChoicesPerQuestion = 2 });

        // Three direct columns, two matrix cells before its guard, two independent sibling iterations.
        result.ColumnCount.Should().Be(7);
        result.Incomplete.Should().ContainSingle();
        result.DuplicateKeys.Should().BeEquivalentTo(["loop__b__ok", "loop__a__bad__0__x"]);
    }

    [Fact]
    public void Flatten_DrivenPanelsShareValueName_RejectsEvenWhenTheTemplateKeysAreDistinct()
    {
        var survey = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"checkbox","name":"source","valuePropertyName":"picked","choices":["a"]},
              {"type":"paneldynamic","name":"first","valueName":"source","templateElements":[{"type":"text","name":"x"}]},
              {"type":"paneldynamic","name":"second","valueName":"source","templateElements":[{"type":"text","name":"y"}]}]}
            """);

        var compile = () => FormDefinitionFlattener.Flatten(survey);

        compile.Should().Throw<ArgumentException>();
        FormDefinitionFlattener.Check(survey, SchemaCompilationLimits.Default).DuplicateKeys.Should().BeEmpty();
    }
}
