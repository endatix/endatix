using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.FormTemplates;

namespace Endatix.Core.Specifications;

public class FormTemplatesSpec : Specification<FormTemplate, FormTemplateDto>
{
    public FormTemplatesSpec(PagingParameters pagingParams, long? folderId)
    {
        if (folderId.HasValue)
        {
            Query.Where(x => x.FolderId == folderId);
        }

        Query
            .OrderByDescending(x => x.CreatedAt)
            .Paginate(pagingParams)
            .AsNoTracking();

        Query.Select(formTemplate =>
            new FormTemplateDto()
            {
                Id = formTemplate.Id.ToString(),
                Name = formTemplate.Name,
                Description = formTemplate.Description,
                CreatedAt = formTemplate.CreatedAt,
                ModifiedAt = formTemplate.ModifiedAt,
                FolderId = formTemplate.FolderId.HasValue ? formTemplate.FolderId.Value.ToString() : null
            });
    }
}
