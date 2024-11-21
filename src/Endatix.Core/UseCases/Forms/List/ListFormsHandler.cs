using Endatix.Core.Entities;
using Endatix.Core.Filters;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.List;

public class ListFormsHandler(IRepository<Form> repository) : IQueryHandler<ListFormsQuery, Result<IEnumerable<FormDto>>>
{
    public async Task<Result<IEnumerable<FormDto>>> Handle(ListFormsQuery request, CancellationToken cancellationToken)
    {
        var pagingFilter = new PagingFilter(request.Page, request.PageSize);
        var spec = new FormsWithSubmissionsCountSpec(pagingFilter);
        IEnumerable<FormDto> forms = await repository.ListAsync(spec, cancellationToken);

        return Result.Success(forms);
    }
}
