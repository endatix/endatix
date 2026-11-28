using System.Text.Json;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Features.FormSchema;

internal sealed class ColumnKeyCheck(IOptions<ReportingOptions> options) : IFormDefinitionCheck
{
    public string Code => "column/duplicate-key";
    public FormCheckStage Stage => FormCheckStage.Draft;

    public Task<IReadOnlyList<FormCheckFinding>> RunAsync(FormCheckInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var limits = SchemaCompilationLimits.Default with { MaxQuestions = options.Value.GetMaxQuestions(input.TenantId) };
        return Task.FromResult(Inspect(input.Definition, limits));
    }

    internal IReadOnlyList<FormCheckFinding> Inspect(JsonElement definition, SchemaCompilationLimits limits)
    {
        var result = FormDefinitionFlattener.Check(definition, limits);
        var findings = result.DuplicateKeys.Select(Duplicate).ToList();
        AddLimit(findings, SchemaCompilationLimitKind.MaxQuestions, (result.QuestionCount, limits.MaxQuestions));
        AddLimit(findings, SchemaCompilationLimitKind.MaxColumns, (result.ColumnCount, limits.MaxColumns));
        AddIncomplete(findings, definition, result);
        return findings;
    }

    private static void AddIncomplete(List<FormCheckFinding> findings, JsonElement definition, FormDefinitionFlattener.ColumnCheck result)
    {
        if (result.Incomplete.Count == 0)
        {
            return;
        }
        foreach (var element in LocatedSurveyElement.In(definition))
        {
            if (result.Incomplete.TryGetValue(element.Node, out var stopped))
            {
                findings.Add(Incomplete(element.Path, stopped));
            }
        }
    }

    private FormCheckFinding Duplicate(string key) => new($"{Code}:{key}", Code, "error", true, false,
        $"Multiple answers generate the column key '{key}'.", new(), JsonSerializer.SerializeToElement(new { columnKey = key }));

    private static FormCheckFinding Incomplete(FindingPath path, SchemaCompilationLimitExceededException limit)
    {
        const string code = "column/check-incomplete";
        return new($"{code}:{path.Element}", code, "error", true, false,
            $"Column keys could not all be checked here ({limit.LimitKind}: {limit.Limit}). Reduce the choices, rows or nested panels in this element, then check again.",
            path, JsonSerializer.SerializeToElement(new { kind = limit.LimitKind.ToString(), limit = limit.Limit }));
    }

    private static void AddLimit(List<FormCheckFinding> findings, SchemaCompilationLimitKind kind, (int Actual, int Limit) count)
    {
        if (count.Actual > count.Limit)
        {
            findings.Add(Limit(kind, count, "Reporting will not compile this form."));
        }
    }

    private static FormCheckFinding Limit(SchemaCompilationLimitKind kind, (int? Actual, int Limit) count, string message)
        => new($"reporting/compile-limit:{kind}", "reporting/compile-limit", "warning", false, true, message,
            new(), JsonSerializer.SerializeToElement(new { kind = kind.ToString(), actual = count.Actual, limit = count.Limit }));
}
