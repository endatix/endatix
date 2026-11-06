using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.Open;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint the respondent's form calls when it opens. It records when the respondent started.
/// </summary>
public sealed class Open(IMediator mediator)
    : Endpoint<OpenSubmissionRequest, Results<Created<SubmissionModel>, Ok<SubmissionModel>, ProblemHttpResult>>
{
    private const string DESCRIPTION =
        "Records that the respondent opened the form. Without a token it creates a submission, in the viewed " +
        "status, through the same checks as a public create. With a submission token, or a share link's " +
        "access token with the submit permission, it resumes that submission. The first open sets startedAt, " +
        "and later opens keep it.";

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/open");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Open a submission";
            s.Description = DESCRIPTION;
            s.Responses[200] = "The submission was resumed.";
            s.Responses[201] = "The submission was created.";
            s.Responses[400] = "Invalid input data, or the token is not valid.";
            s.Responses[403] = "The access token does not have the submit permission.";
            s.Responses[404] = "Form or submission not found.";
            s.Responses[409] = "A submission already exists for this user and form.";
            s.Responses[503] = "The form or submission is busy; try again shortly.";
        });
        Description(Produces);
    }

    private static void Produces(RouteHandlerBuilder builder) => builder
        .Produces<SubmissionModel>(StatusCodes.Status200OK, "application/json")
        .Produces<SubmissionModel>(StatusCodes.Status201Created, "application/json")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    /// <inheritdoc/>
    public override async Task<Results<Created<SubmissionModel>, Ok<SubmissionModel>, ProblemHttpResult>> ExecuteAsync(
        OpenSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new OpenSubmissionCommand(request.FormId, request.SubmissionToken, request.AccessToken, User), ct);
        var mapped = result.Map(SubmissionMapper.Map<SubmissionModel>);
        return mapped.Status switch
        {
            ResultStatus.Created => TypedResults.Created(default(string), mapped.Value),
            ResultStatus.Ok => TypedResults.Ok(mapped.Value),
            _ => mapped.ToProblem(),
        };
    }
}

/// <summary>
/// Request payload for <see cref="Open"/>.
/// </summary>
public sealed class OpenSubmissionRequest
{
    /// <summary>The form the respondent opened.</summary>
    public long FormId { get; set; }

    /// <summary>The token of the submission the respondent resumes. Omit both tokens to create one.</summary>
    public string? SubmissionToken { get; set; }

    /// <summary>A share link's access token, with the submit permission, naming the submission to resume.</summary>
    public string? AccessToken { get; set; }
}

/// <summary>
/// Validation rules for <see cref="OpenSubmissionRequest"/>.
/// </summary>
public sealed class OpenSubmissionValidator : Validator<OpenSubmissionRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSubmissionValidator"/> class.
    /// </summary>
    public OpenSubmissionValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => string.IsNullOrEmpty(x.SubmissionToken) || string.IsNullOrEmpty(x.AccessToken))
            .WithName(nameof(OpenSubmissionRequest.AccessToken))
            .WithMessage("Send a submission token or an access token, not both.");
    }
}
