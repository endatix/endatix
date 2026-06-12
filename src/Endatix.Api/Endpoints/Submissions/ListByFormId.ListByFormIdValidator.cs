using Endatix.Api.Common;
using Endatix.Infrastructure.Features.Submitters;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Endpoints.Submissions;

public class ListByFormIdValidator : Validator<ListByFormIdRequest>
{
    private static readonly Dictionary<string, Type> _filterableFields = new(StringComparer.OrdinalIgnoreCase)
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
        { "completedAt", typeof(DateTime) },
        { "submitterId", typeof(long) },
        { "submitterDisplayId", typeof(string) },
        { "isTestSubmission", typeof(bool) }
    };

    public ListByFormIdValidator(IOptions<SubmitterOptions> submitterOptions)
    {
        Include(new PagedRequestValidator());
        Include(new FilteredRequestValidator(BuildFilterableFields(submitterOptions.Value)));

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }

    private static Dictionary<string, Type> BuildFilterableFields(SubmitterOptions submitterOptions)
    {
        Dictionary<string, Type> filterableFields = new(_filterableFields, StringComparer.OrdinalIgnoreCase);
        foreach (var field in submitterOptions.ProfileSnapshotFields)
        {
            filterableFields[$"submitterProfile.{field}"] = typeof(string);
        }

        return filterableFields;
    }
}
