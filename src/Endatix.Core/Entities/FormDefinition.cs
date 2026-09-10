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
    /// Creates a definition with an explicit Id — for tests, imports, seeding and data migrations
    /// that need the identity fixed up front. <paramref name="id"/> must be positive. The normal path
    /// is the constructor; the EF <c>OnAdd</c> snowflake value generator assigns the Id on add.
    /// </summary>
    public static FormDefinition Create(long id, long tenantId, bool isDraft = false, string? jsonData = null)
    {
        FormDefinition definition = new(tenantId, isDraft, jsonData);
        definition.AssignId(id);
        return definition;
    }

    public IReadOnlyList<Submission> Submissions => _submissions.AsReadOnly();

    public bool IsDraft { get; private set; }
    public string JsonData { get; private set; } = EndatixConfig.Configuration.DefaultFormDefinitionJson;

    public long FormId { get; private set; }

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

    public override void Delete()
    {
        if (!IsDeleted)
        {
            // Delete all related submissions
            foreach (var submission in _submissions)
            {
                submission.Delete();
            }

            // Delete the form definition itself
            base.Delete();
        }
    }
}
