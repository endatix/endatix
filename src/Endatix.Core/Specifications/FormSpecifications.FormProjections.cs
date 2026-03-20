using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Projection specifications for lightweight form reads.
/// </summary>
public static class FormProjections
{
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