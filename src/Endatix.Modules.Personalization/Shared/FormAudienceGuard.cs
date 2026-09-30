using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Personalization.Shared;

/// <summary>
/// Confirms the form exists in the caller's tenant before audience work runs.
/// </summary>
internal static class FormAudienceGuard
{
    public static async Task<Result> EnsureFormExistsAsync(
        IRepository<Form> forms,
        long formId,
        CancellationToken cancellationToken)
    {
        Form? form = await forms.GetByIdAsync(formId, cancellationToken);
        return form is null
            ? Result.NotFound("Form not found.")
            : Result.Success();
    }
}
