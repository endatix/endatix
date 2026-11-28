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
        var result = FormDefinitionFlattener.Check(input.Definition, limits);
        var findings = result.DuplicateKeys.Select(Duplicate).ToList();
        AddLimit(findings, SchemaCompilationLimitKind.MaxQuestions, (result.QuestionCount, limits.MaxQuestions));
        AddLimit(findings, SchemaCompilationLimitKind.MaxColumns, (result.ColumnCount, limits.MaxColumns));
        if (result.ExpansionLimit is { } stopped)
        {
            findings.Add(Limit(stopped.LimitKind, (stopped.Actual, stopped.Limit),
                "Reporting cannot expand this survey. Duplicate key checking is incomplete."));
        }
        return Task.FromResult<IReadOnlyList<FormCheckFinding>>(findings);
    }

    private FormCheckFinding Duplicate(string key) => new($"{Code}:{key}", Code, "error", true, false,
        $"Multiple answers generate the column key '{key}'.", new(), JsonSerializer.SerializeToElement(new { columnKey = key }));

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
