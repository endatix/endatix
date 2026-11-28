using System.Globalization;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;
using MediatR;

namespace Endatix.Core.UseCases.FormDrafts;

internal sealed class CheckDraftHandler(IFormDraftRepository drafts, ITenantContext tenant, ISender sender)
    : IQueryHandler<CheckDraft, Result<FormCheckReport>>
{
    public async Task<Result<FormCheckReport>> Handle(CheckDraft request, CancellationToken cancellationToken)
    {
        var state = await drafts.ReadComparisonAsync(tenant.TenantId, request.FormId, cancellationToken);
        if (!state.IsSuccess)
        {
            return state.ToErrorResult<FormCheckReport>();
        }
        var (version, _, published, draft) = state.Value;
        var json = draft ?? published ?? FormDefinitionDefaults.Json;
        using var document = JsonDocument.Parse(json);
        var hash = state.Value.DraftHash ?? CanonicalContentHash.Of(CanonicalJson.Write(document.RootElement));
        var input = new FormCheckInput(tenant.TenantId, request.FormId, version, hash, document.RootElement);
        return await sender.Send(new CheckFormDefinition(input, FormCheckStage.Draft), cancellationToken);
    }
}

internal sealed class CheckFormDefinitionHandler(IEnumerable<IFormDefinitionCheck> checks, IRepository<CustomQuestion> questions)
    : IQueryHandler<CheckFormDefinition, Result<FormCheckReport>>
{
    public async Task<Result<FormCheckReport>> Handle(CheckFormDefinition request, CancellationToken cancellationToken)
    {
        var selected = checks.Where(check => check.Stage <= request.Stage).ToArray();
        if (MissingCapability(selected, request.Stage) is { } unavailable)
        {
            return Result.Unavailable(unavailable);
        }
        var input = request.Input;
        List<FormCheckFinding> findings = [.. await new UnknownElementTypeCheck(questions).RunAsync(input, cancellationToken)];
        foreach (var check in selected)
        {
            findings.AddRange(await check.RunAsync(input, cancellationToken));
        }
        if (request.Stage == FormCheckStage.Draft)
        {
            findings.Add(new("export/not-checked:", "export/not-checked", "info", false, false,
                "Export names have not been checked.", new()));
        }
        return Report(input, findings);
    }

    private static string? MissingCapability(IFormDefinitionCheck[] selected, FormCheckStage stage)
    {
        if (!selected.Any(check => check.Code == "column/duplicate-key"))
        {
            return "Required draft checks are unavailable. Retry after the host configuration is restored.";
        }
        return stage == FormCheckStage.Publication && !selected.Any(check => check.Code == "export/name-collision")
            ? "Publication export-name checks are unavailable. Retry after the check is installed." : null;
    }

    private static FormCheckReport Report(FormCheckInput input, List<FormCheckFinding> findings)
    {
        var outcome = findings.Any(f => f.RequiresAck) ? "warn" : "pass";
        return new(input.DraftVersion.ToString(CultureInfo.InvariantCulture), input.ContentHash,
            findings.Any(f => f.Blocking) ? "block" : outcome, findings.OrderBy(f => f.Key, StringComparer.Ordinal).ToArray());
    }
}
