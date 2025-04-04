using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public class Theme : TenantEntity, IAggregateRoot
{
    private readonly List<Form> _forms = [];

    private Theme() { } // For EF Core

    public Theme(long tenantId, string name, string? description = null, string? jsonData = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        
        Name = name;
        Description = description;
        JsonData = jsonData ?? "{}"; // Default to empty JSON object
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    
    /// <summary>
    /// The JSON data representing the theme properties.
    /// Uses JSONB in PostgreSQL and NVARCHAR(MAX) in SQL Server.
    /// </summary>
    public string JsonData { get; private set; } = "{}";

    /// <summary>
    /// Forms using this theme
    /// </summary>
    public IReadOnlyCollection<Form> Forms => _forms.AsReadOnly();

    /// <summary>
    /// Updates the theme's name
    /// </summary>
    public void UpdateName(string name)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// Updates the theme's description
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Updates the theme's JSON data
    /// </summary>
    public void UpdateJsonData(string jsonData)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));
        JsonData = jsonData;
    }

    public override void Delete()
    {
        if (!IsDeleted)
        {
            base.Delete();
        }
    }
} 