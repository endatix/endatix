using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.FormTemplates;

namespace Endatix.Core.Specifications;

public class FormTemplatesSpec : Specification<FormTemplate, FormTemplateDto>
{
    public FormTemplatesSpec(PagingParameters pagingParams)
    {
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
                IsEnabled = formTemplate.IsEnabled,
                CreatedAt = formTemplate.CreatedAt,
                ModifiedAt = formTemplate.ModifiedAt
            });
    }
}
