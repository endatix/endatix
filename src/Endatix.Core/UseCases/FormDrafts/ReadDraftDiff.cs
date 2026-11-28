using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.FormDrafts;

/// <summary>Compare the current draft to the form's actual live definition.</summary>
[Experimental("ENDATIX0007")]
public sealed record ReadDraftDiff(long FormId) : IQuery<Result<DraftDiffResponse>>;

/// <summary>PublishedNumber is explicitly null before first publication. Changed documents
/// include both canonical texts for rendering a unified JSON view from the same read.</summary>
[Experimental("ENDATIX0007")]
public sealed record DraftDiffResponse(
    [property: JsonIgnore] long Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? PublishedNumber,
    IReadOnlyList<SurveyChange> Changes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PublishedJson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DraftJson = null);
