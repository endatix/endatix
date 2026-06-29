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
}