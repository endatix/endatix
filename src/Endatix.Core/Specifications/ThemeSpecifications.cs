using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with Theme entities
/// </summary>
public static class ThemeSpecifications
{
    /// <summary>
    /// Specification to get a theme by ID with forms
    /// </summary>
    public sealed class ByIdWithForms : Specification<Theme>, ISingleResultSpecification<Theme>
    {
        public ByIdWithForms(long id)
        {
            Query
                .Where(t => t.Id == id)
                .Include(t => t.Forms);
        }
    }

    /// <summary>
    /// Specification to get a theme by name (case-insensitive)
    /// </summary>
    public sealed class ByName : Specification<Theme>, ISingleResultSpecification<Theme>
    {
        public ByName(string name)
        {
            Query.Where(t => t.Name.ToLower() == name.ToLower());
        }
    }

    /// <summary>
    /// Specification to get themes with name containing filter text
    /// </summary>
    public sealed class WithNameContaining : Specification<Theme>
    {
        public WithNameContaining(string filterText)
        {
            Query.Where(t => t.Name.ToLower().Contains(filterText.ToLower()));
        }
    }
    
    /// <summary>
    /// Specification to get themes with pagination
    /// </summary>
    public sealed class Paginated : Specification<Theme>
    {
        public Paginated(int page, int pageSize)
        {
            Query
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }
    }
} 