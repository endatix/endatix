using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification to find an email template by name.
/// </summary>
public class EmailTemplateByNameSpec : Specification<EmailTemplate>
{
    public EmailTemplateByNameSpec(string name)
    {
        Query.Where(t => t.Name == name);
    }
} 