using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for <see cref="FormTemplate"/> queries.
/// </summary>
public static class FormTemplateSpecifications
{
    /// <summary>
    /// Form templates assigned to a folder.
    /// </summary>
    public sealed class ByFolderId : Specification<FormTemplate>
    {
        public ByFolderId(long folderId)
        {
            Query.Where(t => t.FolderId == folderId);
        }
    }
}
