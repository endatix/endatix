using System.Text.Json;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class ColumnKeyCheckTests
{
    [Fact]
    public void Check_ExpandedColumnBudget_PreservesEarlierKeysAndLaterDirectColumns()
    {
        var definition = JsonSerializer.Deserialize<JsonElement>("""
            {"elements":[{"type":"matrix","name":"m","rows":["r","s"]},
                         {"type":"text","name":"m__r"},{"type":"text","name":"x"},{"type":"text","name":"x"}]}
            """);
        var result = FormDefinitionFlattener.Check(definition, new SchemaCompilationLimits { MaxColumns = 1 });
        result.Incomplete.Values.Should().ContainSingle().Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxColumns);
        result.DuplicateKeys.Should().BeEquivalentTo(["m__r", "x"]);
    }

    [Fact]
    public void Check_NestedLoopCombinationGuard_LocatesTheTruncatedTemplate()
    {
        var definition = FormSchemaFixtureLoader.LoadDefinition("nested-loop-definition.json");
        var result = FormDefinitionFlattener.Check(definition, new SchemaCompilationLimits { MaxLoopCombinations = 1 });
        result.Incomplete.Should().NotBeEmpty();
        var addresses = LocatedSurveyElement.In(definition).Where(e => result.Incomplete.ContainsKey(e.Node)).ToList();
        addresses.Count.Should().Be(result.Incomplete.Count);
        addresses.Should().OnlyContain(e => e.Path.Element!.Contains("/templateElements/"));
        result.Incomplete.Values.Should().OnlyContain(limit => limit.LimitKind == SchemaCompilationLimitKind.MaxLoopCombinations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_BoundedMatrix_BlocksOnlyUnverifiedBranchAndRetainsEmittedKeys(bool siblings)
    {
        var elements = new List<object> { new { type = "matrix", name = "m", rows = Enumerable.Range(0, 2501).Select(i => $"r{i}") } };
        if (siblings)
        {
            elements.AddRange([new { type = "text", name = "m__r0" }, new { type = "text", name = "after" },
                new { type = "text", name = "after" }]);
        }
        var input = new FormCheckInput(1, 2, 3, "hash", JsonSerializer.SerializeToElement(new { elements }));
        var check = new ColumnKeyCheck(Options.Create(new ReportingOptions()));

        var findings = await check.RunAsync(input, TestContext.Current.CancellationToken);
        var repeat = await check.RunAsync(input, TestContext.Current.CancellationToken);

        var guard = findings.Single(f => f.Code == "column/check-incomplete");
        guard.Blocking.Should().BeTrue();
        guard.RequiresAck.Should().BeFalse();
        guard.Path.Element.Should().Be(siblings ? "/elements/index:0" : "/elements/name:m");
        findings.Select(f => f.Key).Should().Equal(repeat.Select(f => f.Key));
        var keys = findings.Where(f => f.Code == "column/duplicate-key").Select(f => f.Key);
        keys.Should().BeEquivalentTo(siblings ? ["column/duplicate-key:m__r0", "column/duplicate-key:after"] : Array.Empty<string>());
    }
}
