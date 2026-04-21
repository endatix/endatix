using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

public class FormDependency : TenantEntity
{
    private FormDependency() { }

    /// <summary>
    /// Creates a new form dependency.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="formId">The form ID.</param>
    /// <param name="dependencyType">The dependency type.</param>
    /// <param name="dependencyIdentifier">The dependency identifier.</param>
    public FormDependency(long tenantId, long formId, FormDependencyType dependencyType, string dependencyIdentifier)
        : base(tenantId)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.Null(dependencyType, nameof(dependencyType));
        Guard.Against.NullOrWhiteSpace(dependencyIdentifier, nameof(dependencyIdentifier));

        FormId = formId;
        DependencyType = dependencyType;
        DependencyIdentifier = dependencyIdentifier;
    }

    public long FormId { get; private set; }
    public Form Form { get; private set; } = null!;
    
    /// <summary>
    /// Dependency target key. Kept as string to support polymorphic targets across dependency types.
    /// </summary>
    public string DependencyIdentifier { get; private set; } = null!;

    public FormDependencyType DependencyType { get; private set; } = FormDependencyType.DataList;
}
