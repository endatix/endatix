using Ardalis.GuardClauses;
using Endatix.Core.Configuration;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class FormDefinition : TenantEntity, IAggregateRoot
{
    private readonly List<Submission> _submissions = [];

    private FormDefinition() { } // For EF Core

    public FormDefinition(long tenantId, bool isDraft = false, string? jsonData = null)
        : base(tenantId)
    {
        jsonData ??= EndatixConfig.Configuration.DefaultFormDefinitionJson;
        IsDraft = isDraft;
        JsonData = jsonData;
    }

    /// <summary>
    /// Explicit Id for tests, imports, seeding. Must be positive. Normal path is the constructor (Id 0 until OnAdd).
    /// </summary>
    public static FormDefinition Create(long id, long tenantId, bool isDraft = false, string? jsonData = null)
    {
        Guard.Against.NegativeOrZero(id);
        FormDefinition definition = new(tenantId, isDraft, jsonData);
        definition.Id = id;
        return definition;
    }

    public IReadOnlyList<Submission> Submissions => _submissions.AsReadOnly();

    public bool IsDraft { get; private set; }
    public string JsonData { get; private set; } = EndatixConfig.Configuration.DefaultFormDefinitionJson;

#pragma warning disable S1144 // EF Core sets it when it loads the definition, and FormDefinitionFactory in the tests.
    public long FormId { get; private set; }
#pragma warning restore S1144

    /// <summary>
    /// Updates the form definition's JSON data with the provided value, or keeps the current data if null is provided.
    /// </summary>
    /// <param name="jsonData">The new JSON data for the form definition, or null to keep the current data.</param>
    public void UpdateSchema(string? jsonData)
    {
        JsonData = jsonData ?? JsonData;
    }

    /// <summary>
    /// Updates the form definition's draft status with the provided value, or keeps the current status if null is provided.
    /// </summary>
    /// <param name="isDraft">The new draft status for the form definition, or null to keep the current status.</param>
    public void UpdateDraftStatus(bool? isDraft)
    {
        IsDraft = isDraft ?? IsDraft;
    }

    /// <summary>
    /// Soft-deletes the definition and the submissions loaded with it. It reaches only those: a form's deletion
    /// deletes all of the form's submissions by form instead (<c>IFormsRepository.DeleteSubmissionsAsync</c>).
    /// </summary>
    public override void Delete()
    {
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
