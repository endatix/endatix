using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Specifications;

public sealed class FormByIdWithSubmissionsCountSpec : SingleResultSpecification<Form, FormDto>
{
    public FormByIdWithSubmissionsCountSpec(long formId)
    {
        Query
            .Where(form => form.Id == formId);

        Query.Select(FormProjections.ToFormDtoWithSubmissionsCount);
    }
}
