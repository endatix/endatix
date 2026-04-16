using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

public sealed class FormByIdWithSubmissionsCountSpec : Specification<Form, FormDto>
{
    public FormByIdWithSubmissionsCountSpec(long formId)
    {
        Query
            .Where(form => form.Id == formId);

        Query.Select(form =>
            new FormDto()
            {
                Id = form.Id.ToString(),
                Name = form.Name,
                Description = form.Description,
                IsEnabled = form.IsEnabled,
                IsPublic = form.IsPublic,
                ThemeId = form.ThemeId.HasValue ? form.ThemeId.Value.ToString() : null,
                ActiveDefinitionId = form.ActiveDefinitionId.HasValue ? form.ActiveDefinitionId.Value.ToString() : null,
                CreatedAt = form.CreatedAt,
                ModifiedAt = form.ModifiedAt,
                SubmissionsCount = form.FormDefinitions.SelectMany(fd => fd.Submissions).Count(),
                WebHookSettingsJson = form.WebHookSettingsJson
            });
    }
}
