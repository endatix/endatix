using System.Text.Json;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Reporting.Features.FormSchema;

internal sealed class UnknownReferenceCheck(IEnumerable<IAudiencePropertyCatalog> catalogs) : IFormDefinitionCheck
{
    public string Code => "reference/unknown";
    public FormCheckStage Stage => FormCheckStage.Publication;

    public async Task<IReadOnlyList<FormCheckFinding>> RunAsync(FormCheckInput input, CancellationToken cancellationToken)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var catalog in catalogs)
        {
            var properties = await catalog.ListForFormAsync(input.TenantId, input.FormId, cancellationToken);
            declared.UnionWith(properties.Select(p => p.VariableName));
        }
        var warn = declared.Count != 0;
        var roots = SurveyReferenceRoots.Unknown(input.Definition, declared);
        return roots.Select(pair => Finding(pair.Key, pair.Value, warn)).ToArray();
    }

    private FormCheckFinding Finding(string root, int count, bool warn) => new(
        $"{Code}:{root}", Code, warn ? "warning" : "info", false, warn,
        $"The survey refers to undeclared '{root}' {count} time(s).", new(),
        JsonSerializer.SerializeToElement(new { root, count }));
}
