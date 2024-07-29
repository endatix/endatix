using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Filters;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.List;

public class ListFormsHandler(IRepository<Form> _repository) : IQueryHandler<ListFormsQuery, Result<IEnumerable<Form>>>
{
    public async Task<Result<IEnumerable<Form>>> Handle(ListFormsQuery request, CancellationToken cancellationToken)
    {
        var pagingFilter = new PagingFilter(request.Page, request.PageSize);
        var spec = new FormsSpec(pagingFilter);
        IEnumerable<Form> forms = await _repository.ListAsync(spec, cancellationToken);
        return Result.Success(forms);
    }
}
