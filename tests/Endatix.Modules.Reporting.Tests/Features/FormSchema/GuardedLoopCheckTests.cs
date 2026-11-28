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
        var check = new ColumnKeyCheck(Options.Create(new ReportingOptions()));
        var findings = check.Inspect(survey, SchemaCompilationLimits.Default);
        findings.Select(f => f.Key).Should().BeEquivalentTo([
            "panel/duplicate-value-name:/elements/name:first/valueName",
            "panel/duplicate-value-name:/elements/name:second/valueName"]);
        findings.Should().OnlyContain(f => f.Blocking && !f.RequiresAck && f.Path.Property == "valueName");
    }

    [Fact]
    public void Flatten_SourceLoopPaths_EnforcesDepthAcrossNamedPanelsAndAllowsSiblingReuse()
    {
        var survey = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"radiogroup","name":"s1","choices":["a"]},{"type":"radiogroup","name":"s2","choices":["b"]},
             {"type":"paneldynamic","name":"left","loopSource":["s1"],"templateElements":[{"name":"shared","type":"text"}]},
             {"type":"paneldynamic","name":"right","loopSource":["s2"],"templateElements":[{"name":"shared","type":"text"}]},
             {"type":"paneldynamic","name":"shared","loopSource":["s1"],"templateElements":[{"name":"q","type":"text"}]}]}
            """);
        var limit = new SchemaCompilationLimits { MaxNestingDepth = 1 };

        var compile = () => FormDefinitionFlattener.Flatten(survey, limit);

        var stopped = compile.Should().Throw<SchemaCompilationLimitExceededException>().Which;
        stopped.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxNestingDepth);
        stopped.Actual.Should().Be(2);
        stopped.Context.Should().Be("shared");
        var checkedSurvey = FormDefinitionFlattener.Check(survey, limit);
        checkedSurvey.Incomplete.Values.Should().ContainSingle().Which.Context.Should().Be("shared");
        var admitted = limit with { MaxNestingDepth = 2 };
        FormDefinitionFlattener.Flatten(survey, admitted).Select(c => c.Key)
            .Should().Contain(["shared__a__a__q", "shared__b__a__q"]);
        FormDefinitionFlattener.Check(survey, admitted).Incomplete.Should().BeEmpty();
    }
}
