using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

/// <summary>
/// Projection specifications for lightweight form reads.
/// </summary>
public static class FormProjections
{
    /// <summary>
    /// Maps a form to <see cref="FormDto"/> with submission statistics.
    /// Keep this as an expression so EF can translate it to SQL in specifications.
    /// </summary>
    public static readonly Expression<Func<Form, FormDto>> ToFormDtoWithSubmissionsCount = form => new FormDto
    {
        Id = form.Id.ToString(),
        Name = form.Name,
        Description = form.Description,
        IsEnabled = form.IsEnabled,
        IsPublic = form.IsPublic,
        LimitOnePerUser = form.LimitOnePerUser,
        SubmissionTokenExpiryHours = form.SubmissionTokenExpiryHours,
        PdfUsesFormTheme = form.PdfUsesFormTheme,
        Metadata = form.Metadata,
        ThemeId = form.ThemeId.HasValue ? form.ThemeId.Value.ToString() : null,
        ActiveDefinitionId = form.ActiveDefinitionId.HasValue ? form.ActiveDefinitionId.Value.ToString() : null,
        CreatedAt = form.CreatedAt,
        ModifiedAt = form.ModifiedAt,
        SubmissionsCount = form.FormDefinitions.SelectMany(fd => fd.Submissions).Count(),
        WebHookSettingsJson = form.WebHookSettingsJson,
        FolderId = form.FolderId.HasValue ? form.FolderId.Value.ToString() : null
    };

    /// <summary>
    /// Routing projection for public form access checks.
    /// </summary>
    public sealed record FormAccessRoutingDto(bool IsPublic, bool LimitOnePerUser, long Id);

    /// <summary>
    /// Minimal projection for resolving session token TTL (form override only).
    /// </summary>
    public sealed record FormSessionTokenExpiryDto(long FormId, int? SubmissionTokenExpiryHours);

    /// <summary>
    /// Permission focused projection for routing public form access checks.
    /// </summary>
    public sealed class AccessRoutingDtoSpec : Specification<Form, FormAccessRoutingDto>
    {
        /// <summary>
        /// Constructor for the AccessRoutingDtoSpec.
        /// </summary>
        /// <param name="formId">The id of the form to get routing metadata for.</param>
        public AccessRoutingDtoSpec(long formId)
        {
            Query
                .Where(form => form.Id == formId && form.IsEnabled)
                .AsNoTracking();

            Query.Select(form => new FormAccessRoutingDto(
                form.IsPublic,
                form.LimitOnePerUser,
                form.Id));
        }
    }

    /// <summary>
    /// What a form access token is checked against on every use: the form's tenant, visibility and enabled state,
    /// and which definition is active and at which version.
    /// </summary>
    /// <param name="Id">The form.</param>
    /// <param name="TenantId">The form's tenant.</param>
    /// <param name="IsPublic">Whether anyone may open the form.</param>
    /// <param name="IsEnabled">Whether the form accepts respondents.</param>
    /// <param name="ActiveDefinitionId">The definition the runtime serves; <c>null</c> when the form has none.</param>
    /// <param name="ActiveDefinitionVersion">
    /// When the active definition was last saved (<c>ModifiedAt</c>, else <c>CreatedAt</c>). Definitions are edited in
    /// place, so this tells a cached analysis of the definition from a stale one.
    /// </param>
    public sealed record FormRuntimeStateDto(
        long Id,
        long TenantId,
        bool IsPublic,
        bool IsEnabled,
        long? ActiveDefinitionId,
        DateTime? ActiveDefinitionVersion);

    /// <summary>
    /// Reads <see cref="FormRuntimeStateDto"/> for a form that is not deleted, in any tenant, by its key: the caller
    /// compares the tenant with the one its token names.
    /// </summary>
    public sealed class RuntimeStateSpec : Specification<Form, FormRuntimeStateDto>, ISingleResultSpecification<Form, FormRuntimeStateDto>
    {
        /// <param name="formId">The form whose runtime state to read.</param>
        public RuntimeStateSpec(long formId)
        {
            Query
                .IgnoreQueryFilters()
                .Where(form => form.Id == formId && !form.IsDeleted)
                .AsNoTracking();

            Query.Select(form => new FormRuntimeStateDto(
                form.Id,
                form.TenantId,
                form.IsPublic,
                form.IsEnabled,
                form.ActiveDefinitionId,
                form.ActiveDefinition != null
                    ? form.ActiveDefinition.ModifiedAt ?? form.ActiveDefinition.CreatedAt
                    : null));
        }
    }

    /// <summary>
    /// Lightweight read of <see cref="Form.SubmissionTokenExpiryHours"/> for token obtain/extend.
    /// </summary>
    public sealed class SessionTokenExpiryDtoSpec : Specification<Form, FormSessionTokenExpiryDto>
    {
        /// <param name="formId">Form whose session TTL override to read.</param>
        public SessionTokenExpiryDtoSpec(long formId)
        {
            Query
                .Where(form => form.Id == formId)
                .AsNoTracking();

            Query.Select(form => new FormSessionTokenExpiryDto(
                form.Id,
                form.SubmissionTokenExpiryHours));
        }
    }
}
