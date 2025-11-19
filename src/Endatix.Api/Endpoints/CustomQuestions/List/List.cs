using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.CustomQuestions.List;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Endpoint for listing custom questions.
/// </summary>
public class List(IMediator mediator) : Endpoint<EmptyRequest, Results<Ok<IEnumerable<CustomQuestionModel>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("questions");
        Permissions(Actions.Questions.View);
        Summary(s =>
        {
            s.Summary = "List custom questions";
            s.Description = "Lists all custom questions for the current tenant.";
            s.Responses[200] = "Custom questions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<CustomQuestionModel>>, BadRequest>> ExecuteAsync(EmptyRequest request, CancellationToken cancellationToken)
    {
        var query = new ListCustomQuestionsQuery();
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, CustomQuestionMapper.Map<CustomQuestionModel>)
            .SetTypedResults<Ok<IEnumerable<CustomQuestionModel>>, BadRequest>();
    }
} 