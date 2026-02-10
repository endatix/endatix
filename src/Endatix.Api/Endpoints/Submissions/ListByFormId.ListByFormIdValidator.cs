using Endatix.Api.Common;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ListByFormIdValidator : Validator<ListByFormIdRequest>
{
    private static readonly Dictionary<string, Type> _filterableFields = new()
    {
        { "id", typeof(long) },
        { "createdAt", typeof(DateTime) },
        { "updatedAt", typeof(DateTime) },
        { "isComplete", typeof(bool) },
        { "status", typeof(string) },
        { "jsonData", typeof(string) },
        { "formId", typeof(long) },
        { "formDefinitionId", typeof(long) },
        { "currentPage", typeof(int) },
        { "metadata", typeof(string) },
        { "completedAt", typeof(DateTime) }
    };

    public ListByFormIdValidator()
    {
        Include(new PagedRequestValidator());
        Include(new FilteredRequestValidator(_filterableFields));

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
