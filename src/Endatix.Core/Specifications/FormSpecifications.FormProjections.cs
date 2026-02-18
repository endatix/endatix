using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public static class FormProjections
{
    /// <summary>
    /// Permission focused projection to get the IsPublic property of a form with its id
    /// </summary>
    public sealed class IsPublicDtoSpec : Specification<Form, FormDtos.IsPublicDto>
    {
        public IsPublicDtoSpec()
        {
            Query.Select(form => new FormDtos.IsPublicDto(form.IsPublic, form.Id));
        }
    }
}