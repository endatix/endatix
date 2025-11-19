using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.CustomQuestions.Create;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Endpoint for creating a new custom question.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateCustomQuestionRequest, Results<Created<CreateCustomQuestionResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("questions");
        Permissions(Actions.Questions.Create);
        Summary(s =>
        {
            s.Summary = "Create a new custom question";
            s.Description = "Creates a new custom question with the provided data.";
            s.Responses[201] = "Custom question created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateCustomQuestionResponse>, BadRequest>> ExecuteAsync(CreateCustomQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateCustomQuestionCommand(
            request.Name!,
            request.JsonData!,
            request.Description);
        var result = await mediator.Send(command, cancellationToken);
        
        return TypedResultsBuilder
            .MapResult(result, CustomQuestionMapper.Map<CreateCustomQuestionResponse>)
            .SetTypedResults<Created<CreateCustomQuestionResponse>, BadRequest>();
    }
}
