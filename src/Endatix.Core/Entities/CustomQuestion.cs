using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;
using System.Text.Json;

namespace Endatix.Core.Entities;

public class CustomQuestion : TenantEntity, IAggregateRoot
{
    private CustomQuestion() { } // For EF Core

    public CustomQuestion(long tenantId, string name, string jsonData, string? description = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));

        Name = name;
        Description = description;
        JsonData = jsonData;
    }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>
    /// The JSON data representing the custom question properties.
    /// Uses JSONB in PostgreSQL and NVARCHAR(MAX) in SQL Server.
    /// </summary>
    public string JsonData { get; private set; } = null!;

    /// <summary>
    /// Updates the custom question's name
    /// </summary>
    public void UpdateName(string name)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// Updates the custom question's description
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Updates the custom question's JSON data
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