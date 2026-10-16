using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Properties;

internal static class PropertyOrder
{
    /// <summary>
    /// The order the Audience tab lists properties in. A reorder does not renumber the others, so two properties can
    /// share a sort order; the older one comes first.
    /// </summary>
    public static IOrderedQueryable<Property> InTabOrder(this IQueryable<Property> properties) =>
        properties
            .OrderBy(property => property.SortOrder)
            .ThenBy(property => property.Id);
}
