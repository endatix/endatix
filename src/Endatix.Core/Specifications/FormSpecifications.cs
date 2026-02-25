using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with Form entities
/// </summary>
public static class FormSpecifications
{
    /// <summary>
    /// Specification to get a form by ID including related entities
    /// </summary>
    public sealed class ByIdWithRelated : Specification<Form>
    {
        public ByIdWithRelated(long id)
        {
            Query
                .Where(f => f.Id == id)
                .Include(f => f.FormDefinitions)
                .Include(f => f.ActiveDefinition)
                .Include(f => f.Theme);
        }
    }


    /// <summary>
    /// Specification to get a form by ID
    /// </summary>
    public sealed class ById : Specification<Form>
    {
        public ById(long id)
        {
            Query.Where(f => f.Id == id);
        }
    }

    /// <summary>
    /// Specification to get forms by theme ID
    /// </summary>
    public sealed class ByThemeId : Specification<Form>
    {
        public ByThemeId(long themeId)
        {
            Query
                .Where(f => f.ThemeId == themeId);
        }
    }

    /// <summary>
    /// Specification to get forms with theme information
    /// </summary>
    public sealed class WithTheme : Specification<Form>
    {
        public WithTheme()
        {
            Query.Include(f => f.Theme);
        }
    }

    /// <summary>
    /// Specification to get forms by name containing a search string
    /// </summary>
    public sealed class ByNameContaining : Specification<Form>
    {
        public ByNameContaining(string searchString)
        {
            Query.Where(f => f.Name.ToLower().Contains(searchString.ToLower()));
        }
    }
}