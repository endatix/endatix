using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Microsoft.Extensions.Logging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Features.Email;
namespace Endatix.Api.Endpoints.Integrations;

/// <summary>
/// Endpoint for receiving the slack token.
/// </summary>
public class SlackToken(ILogger<SlackToken> logger, IEmailSender emailSender) : Endpoint<SlackTokenRequest, Results<Ok<string>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("slacktoken");
        AllowAnonymous();
        Tags("hidden"); // With this tag, the endpoint is hidden from the API documentation
        Summary(s =>
        {
            s.Summary = "Receives a Slack token";
            s.Description = "Receives a Slack token";
            s.Responses[200] = "Token received successfully.";
            s.Responses[400] = "Invalid input data.";
            s.ResponseExamples[200] = "ok";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(SlackTokenRequest request, CancellationToken ct)
    {
        logger.LogInformation($"Received slack token: {request.Token}");

        EmailWithBody email = new EmailWithBody()
        {
            To = "info@endatix.com",
            From = "info@endatix.com",
            Subject = "Token",
            HtmlBody = request.Token,
            PlainTextBody = request.Token
        };

        await emailSender.SendEmailAsync(email, ct);

        var operationResult = Result.Success("ok");

        return TypedResultsBuilder
            .FromResult(operationResult)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
