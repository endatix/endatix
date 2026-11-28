using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

public partial class FormDefinition : TenantEntity, IAggregateRoot, IContentVersion
{
    private readonly List<Submission> _submissions = [];

    private FormDefinition() { } // For EF Core

    public FormDefinition(long tenantId, bool isDraft = false, string? jsonData = null)
        : base(tenantId)
    {
        jsonData ??= FormDefinitionDefaults.Json;
        IsDraft = isDraft;
        JsonData = jsonData;
    }

    /// <summary>
    /// Explicit Id for tests, imports, seeding. Must be positive. Normal path is the constructor (Id 0 until OnAdd).
    /// </summary>
    public static FormDefinition Create(long id, long tenantId, bool isDraft = false, string? jsonData = null)
    {
        Guard.Against.NegativeOrZero(id);
        FormDefinition definition = new(tenantId, isDraft, jsonData)
        {
            Id = id
        };
        return definition;
    }

    public IReadOnlyList<Submission> Submissions => _submissions.AsReadOnly();

    public bool IsDraft { get; private set; }

    /// <summary>Survey JSON as published, or the editable legacy document.</summary>
    public string JsonData
    {
        get => _publishedJson ?? field;
        private set;
    } = FormDefinitionDefaults.Json;

    public long FormId { get; private set; }

    /// <summary>
    /// Updates the form definition's JSON data with the provided value, or keeps the current data if null is provided.
    /// </summary>
    /// <param name="jsonData">The new JSON data for the form definition, or null to keep the current data.</param>
    public void UpdateSchema(string? jsonData)
    {
        EnsureEditable();
        JsonData = jsonData ?? JsonData;
    }

    /// <summary>
    /// Updates the form definition's draft status with the provided value, or keeps the current status if null is provided.
    /// </summary>
    /// <param name="isDraft">The new draft status for the form definition, or null to keep the current status.</param>
    public void UpdateDraftStatus(bool? isDraft)
    {
        EnsureEditable();
        IsDraft = isDraft ?? IsDraft;
    }

    /// <summary>
    /// Soft-deletes the definition and the submissions loaded with it. It reaches only those: a form's deletion
    /// deletes all of the form's submissions by form instead (<c>IFormsRepository.DeleteSubmissionsAsync</c>).
    /// </summary>
    public override void Delete()
    {
        EnsureEditable();
        if (!IsDeleted)
        {
            // Only the submissions loaded with the definition.
            foreach (var submission in _submissions)
            {
                submission.Delete();
            }

            // Delete the form definition itself
            base.Delete();
        }
    }
}
