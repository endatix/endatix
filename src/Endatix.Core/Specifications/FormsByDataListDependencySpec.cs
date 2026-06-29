using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

/// <summary>
/// Forms that declare a dependency on the given data list, projected to <see cref="FormDto"/>.
/// </summary>
public sealed class FormsByDataListDependencySpec : Specification<Form, FormDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormsByDataListDependencySpec"/> class.
    /// </summary>
    /// <param name="dataListId">The ID of the data list to get the dependent forms for.</param>
    public FormsByDataListDependencySpec(long dataListId)
    {
        var identifier = dataListId.ToString();
        Query.Where(form => form.Dependencies.Any(dependency =>
            dependency.DependencyType == FormDependencyType.DataList &&
            dependency.DependencyIdentifier == identifier));
        Query.OrderByDescending(form => form.CreatedAt);
        Query.AsNoTracking();
        Query.Select(FormProjections.ToFormDtoWithSubmissionsCount);
    }
}
