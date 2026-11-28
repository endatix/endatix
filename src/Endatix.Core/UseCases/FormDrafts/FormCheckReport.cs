using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDrafts;

/// <summary>Check the current, admitted draft without creating a head.</summary>
[Experimental("ENDATIX0007")]
public sealed record CheckDraft(long FormId) : IQuery<Result<FormCheckReport>>;

/// <summary>Check a particular document, including a publication's frozen copy.</summary>
[Experimental("ENDATIX0007")]
public sealed record CheckFormDefinition(FormCheckInput Input, FormCheckStage Stage) : IQuery<Result<FormCheckReport>>;

/// <summary>The document and identity must come from the same draft or publication.</summary>
[Experimental("ENDATIX0007")]
public sealed record FormCheckInput(long TenantId, long FormId, long DraftVersion, string ContentHash, JsonElement Definition);

/// <summary>Publication checking includes the checks run when opening the dialog.</summary>
[Experimental("ENDATIX0007")]
public enum FormCheckStage { Draft, Publication }

/// <summary>A server check and the earliest stage at which it runs.</summary>
[Experimental("ENDATIX0007")]
public interface IFormDefinitionCheck
{
    string Code { get; }
    FormCheckStage Stage { get; }
    Task<IReadOnlyList<FormCheckFinding>> RunAsync(FormCheckInput input, CancellationToken cancellationToken);
}

/// <summary>The report shared by the dialog and publication resource.</summary>
[Experimental("ENDATIX0007")]
public sealed record FormCheckReport(string DraftVersion, string ContentHash, string Outcome, IReadOnlyList<FormCheckFinding> Findings);

/// <summary>A finding keyed by code and location, independent of localized copy.</summary>
[Experimental("ENDATIX0007")]
public sealed record FormCheckFinding(string Key, string Code, string Severity, bool Blocking, bool RequiresAck,
    string Message, FindingPath Path, JsonElement? Data = null)
{
    public string Origin { get; init; } = "server";
}

/// <summary>Document locations use the same escaped selectors as the draft comparison.</summary>
[Experimental("ENDATIX0007")]
public sealed record FindingPath(string? Page = null, string? Element = null, string? Choice = null, string? Property = null);
