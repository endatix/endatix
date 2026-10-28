using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;

namespace Endatix.Modules.Reporting.Features.SubmissionViews;

/// <summary>Reads the submissions grid view everyone on a form sees.</summary>
public sealed record GetSubmissionViewQuery(long TenantId, long FormId) : IQuery<Result<SubmissionViewDto>>;

/// <summary>
/// Returns the form's shared view without the columns the form no longer has, or the defaults when nobody saved one.
/// </summary>
internal sealed class GetSubmissionViewHandler(
    IRepository<Form> formsRepository,
    ISubmissionViewRepository viewRepository,
    SubmissionViewReader viewReader)
    : IQueryHandler<GetSubmissionViewQuery, Result<SubmissionViewDto>>
{
    /// <inheritdoc />
    public async Task<Result<SubmissionViewDto>> Handle(
        GetSubmissionViewQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var (tenantId, formId) = request;
        var formExists = await formsRepository.AnyAsync(
            new FormSpecifications.ByIdForTenant(formId, tenantId),
            cancellationToken);

        return formExists
            ? Result.Success(await ReadAsync(tenantId, formId, cancellationToken))
            : Result.NotFound("Form not found.");
    }

    private async Task<SubmissionViewDto> ReadAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var view = await viewRepository.FindSharedAsync(tenantId, formId, cancellationToken);
        if (view is null)
        {
            return SubmissionViewDto.Default;
        }

        return await viewReader.ReadAsync(view, cancellationToken);
    }
}
