using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.FormSchema;

internal sealed record AuthorizeSchemaCompile(long FormId, long TenantId) : IQuery<Result<bool>>;

internal sealed class AuthorizeSchemaCompileHandler(IRepository<Form> forms, AccountStanding standing)
    : IQueryHandler<AuthorizeSchemaCompile, Result<bool>>
{
    public async Task<Result<bool>> Handle(AuthorizeSchemaCompile request, CancellationToken cancellationToken)
    {
        // The command still rechecks its target before processing, including when this preflight is dormant.
        if (!standing.IsEnforced)
        {
            return Result.Success(true);
        }

        var target = await forms.FirstOrDefaultAsync(new VisibleDefinition(request), cancellationToken);
        if (target is null)
        {
            return Result.NotFound("Form not found.");
        }

        return target.HasDefinition
            ? await standing.RefusalAsync<bool>(cancellationToken) ?? Result.Success(true)
            : Result.NotFound("Active form definition not found.");
    }

    private sealed record Target(bool HasDefinition);

    private sealed class VisibleDefinition : Specification<Form, Target>
    {
        public VisibleDefinition(AuthorizeSchemaCompile request)
        {
            Query.Where(f => f.Id == request.FormId && f.TenantId == request.TenantId && request.TenantId > 0)
                .Select(f => new Target(f.ActiveDefinition != null));
        }
    }
}
