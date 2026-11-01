using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>Whether a form exists in a tenant, for the grid's not-found answers.</summary>
internal static class TenantForms
{
    internal const string FormNotFoundMessage = "Form not found.";

    internal static async Task<bool> ExistsAsync(
        this IRepository<Form> forms,
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        var form = await forms.SingleOrDefaultAsync(new FormSpecifications.ByIdReadOnly(formId), cancellationToken);
        return form is not null && form.TenantId == tenantId;
    }
}
