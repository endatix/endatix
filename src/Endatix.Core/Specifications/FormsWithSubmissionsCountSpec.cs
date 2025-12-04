using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

public sealed class FormsWithSubmissionsCountSpec : Specification<Form, FormDto>
{
    public FormsWithSubmissionsCountSpec(PagingParameters pagingParams, FilterParameters filterParams)
    {
        Query
            .Filter(filterParams)
            .OrderByDescending(x => x.CreatedAt)
            .Paginate(pagingParams)
            .AsNoTracking();

        Query.Select(form =>
            new FormDto()
            {
                Id = form.Id.ToString(),
                Name = form.Name,
                Description = form.Description,
                IsEnabled = form.IsEnabled,
                CreatedAt = form.CreatedAt,
                ModifiedAt = form.ModifiedAt,
                SubmissionsCount = form.FormDefinitions.SelectMany(fd => fd.Submissions).Count()
            });
    }
}

