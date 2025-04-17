using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Form : TenantEntity, IAggregateRoot
{
    private readonly List<FormDefinition> _formDefinitions = [];

    private Form() { } // For EF Core

    public Form(long tenantId, string name, string? description = null, bool isEnabled = false)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, null, "Form name cannot be null.");
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
    }

    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }

    public long? ActiveDefinitionId { get; private set; }
    public FormDefinition? ActiveDefinition { get; private set; }

    public long? ThemeId { get; private set; }
    public Theme? Theme { get; private set; }

    public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();

    public void SetActiveFormDefinition(FormDefinition formDefinition)
    {
        Guard.Against.Null(formDefinition, nameof(formDefinition));

        if (!_formDefinitions.Contains(formDefinition))
        {
            throw new InvalidOperationException("Cannot set a FormDefinition as active that doesn't belong to this form.");
        }

        ActiveDefinition = formDefinition;
    }

    public void AddFormDefinition(FormDefinition formDefinition, bool isActive = true)
    {
        _formDefinitions.Add(formDefinition);

        if (isActive && _formDefinitions.Count == 1)
        {
            SetActiveFormDefinition(formDefinition);
        }
    }
    
    public void SetTheme(Theme? theme)
    {
        Theme = theme;
        ThemeId = theme?.Id;
    }

    public override void Delete()
    {
        if (!IsDeleted)
        {
            // Delete all related form definitions
            foreach (var definition in _formDefinitions)
            {
                definition.Delete();
            }

            // Delete the form itself
            base.Delete();
        }
    }
}
