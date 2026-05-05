using Ardalis.GuardClauses;
using Endatix.Core.Configuration;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class FormTemplate : TenantEntity, IAggregateRoot
{
    private FormTemplate() { } // For EF Core

    public FormTemplate(long tenantId, string name, string? description = null, string? jsonData = null, long? folderId = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, null, "Form Template name cannot be null.");
        jsonData ??= EndatixConfig.Configuration.DefaultFormDefinitionJson;
        
        Name = name;
        Description = description;
        JsonData = jsonData;
        FolderId = folderId;
    }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string JsonData { get; set; } = EndatixConfig.Configuration.DefaultFormDefinitionJson;

    public long? FolderId { get; set; }
    public Folder? Folder { get; set; }
}
