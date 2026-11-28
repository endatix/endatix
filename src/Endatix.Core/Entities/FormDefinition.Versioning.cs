using Ardalis.GuardClauses;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

internal sealed record PublishedDraft(long Number, long Version, string Json);

public partial class FormDefinition
{
    /// <summary>Published sequence number; an old writer can still insert an unnumbered row.</summary>
    public long? Number { get; private set; }
    /// <summary>The draft version frozen at publication. Legacy rows have no publish provenance.</summary>
    public long? PublishedFromDraftVersion { get; private set; }
    public long? CreatedBy { get; private set; }
    public string Source { get; private set; } = ContentVersionSources.PUBLISH;
    public long? RestoredFromNumber { get; internal init; }

    // Old hosts can still edit legacy JSON without updating a cached hash. Hash its actual content.
    public string ContentHash { get => field ?? CanonicalSurveyJson.PublishedHash(JsonData); private set; }

    // Native JSON columns support existing SQL exports, but can normalize numeric tokens.
    // Keep the exact published survey alongside that representation for editing, hashing and replay.
    private string? _publishedJson;

    long IContentVersion.Number => Number ?? throw new InvalidOperationException("Definition has not been numbered.");
    string IContentVersion.Json => CanonicalJson.Write(JsonData);

    internal static FormDefinition FromDraft(Form form, PublishedDraft draft, ContentVersionStamp stamp)
    {
        Guard.Against.NegativeOrZero(form.Id);
        Guard.Against.NegativeOrZero(draft.Number);
        Guard.Against.Negative(draft.Version);
        return new FormDefinition(form.TenantId, jsonData: draft.Json)
        {
            FormId = form.Id,
            Number = draft.Number,
            PublishedFromDraftVersion = draft.Version,
            CreatedBy = stamp.By,
            CreatedAt = stamp.At,
            ContentHash = CanonicalSurveyJson.Hash(draft.Json),
            _publishedJson = draft.Json
        };
    }

    private void EnsureEditable()
    {
        if (PublishedFromDraftVersion.HasValue)
        {
            throw new InvalidOperationException("Published definitions are insert-only.");
        }
    }
}
