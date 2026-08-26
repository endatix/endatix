using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.List;

namespace Endatix.Core.Specifications;

public class FormTemplatesSpec : Specification<FormTemplate, FormTemplateDto>
{
    public FormTemplatesSpec(
        PagingParameters pagingParams,
        FilterParameters filterParams,
        FormTemplateListSortBy sortBy = FormTemplateListSortBy.CreatedAt,
        bool sortDescending = true,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? modifiedFrom = null,
        DateTime? modifiedTo = null)
    {
        Query
            .Filter(filterParams)
            .WhereCreatedRange(createdFrom, createdTo)
            .WhereModifiedRange(modifiedFrom, modifiedTo);

        ApplyOrdering(Query, sortBy, sortDescending);

        Query
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

    private static void ApplyOrdering(
        ISpecificationBuilder<FormTemplate> query,
        FormTemplateListSortBy sortBy,
        bool sortDescending)
    {
        switch (sortBy)
        {
            case FormTemplateListSortBy.Name:
                query.OrderByWithIdTiebreaker(x => x.Name, sortDescending);
                break;
            case FormTemplateListSortBy.ModifiedAt:
                query.OrderByWithIdTiebreaker(x => x.ModifiedAt, sortDescending);
                break;
            default:
                query.OrderByWithIdTiebreaker(x => x.CreatedAt, sortDescending);
                break;
        }
    }
}
