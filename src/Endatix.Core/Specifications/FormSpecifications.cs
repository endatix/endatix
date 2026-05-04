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
    /// Loads a form by id for public URL flows (share/embed, mint form access token) where the request may carry
    /// a different tenant context than the form's tenant. Ignores global tenant filters; still excludes soft-deleted rows.
    /// </summary>
    public sealed class ByIdWithRelatedForPublicAccess : Specification<Form>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByIdWithRelatedForPublicAccess"/> class.
        /// </summary>
        /// <param name="id">The ID of the form to get.</param>
        public ByIdWithRelatedForPublicAccess(long id)
        {
            Query
                .IgnoreQueryFilters()
                .Where(f => f.Id == id && !f.IsDeleted)
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