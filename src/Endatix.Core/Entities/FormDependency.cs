using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;


/// <summary>
/// Entity representing a form dependency to track which items are used in the form - data lists, images, etc.
/// </summary>
public class FormDependency : TenantEntity
{
    // Private constructor for EF Core
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
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.Null(dependencyType);
        Guard.Against.NullOrWhiteSpace(dependencyIdentifier);

        FormId = formId;
        DependencyType = dependencyType;
        DependencyIdentifier = dependencyIdentifier;
    }

    /// <summary>
    /// The form ID.
    /// </summary>
    public long FormId { get; private set; }

    /// <summary>
    /// The form.
    /// </summary>
    public Form Form { get; private set; } = null!;
    
    /// <summary>
    /// Dependency target key. Kept as string to support polymorphic targets across dependency types.
    /// </summary>
    public string DependencyIdentifier { get; private set; } = null!;

    /// <summary>
    /// The type of the dependency.
    /// </summary>
    public FormDependencyType DependencyType { get; private set; } = FormDependencyType.DataList;
}
