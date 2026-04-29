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
        WebHookSettingsJson = form.WebHookSettingsJson
    };

    /// <summary>
    /// Permission focused projection to get the IsPublic property of a form with its id
    /// </summary>
    public sealed class IsPublicDtoSpec : Specification<Form, FormDtos.IsPublicDto>
    {
        /// <summary>
        /// Constructor for the IsPublicDtoSpec
        /// </summary>
        /// <param name="formId">The id of the form to get the IsPublic property for</param>
        public IsPublicDtoSpec(long formId)
        {
            Query.Where(form => form.Id == formId);
            Query.Select(form => new FormDtos.IsPublicDto(form.IsPublic, form.Id));
            Query.AsNoTracking();
        }
    }   
}