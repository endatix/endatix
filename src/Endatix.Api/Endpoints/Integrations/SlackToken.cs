using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Microsoft.Extensions.Logging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Features.Email;
namespace Endatix.Api.Endpoints.Integrations;

/// <summary>
/// Endpoint for receiving the slack token.
/// </summary>
public class SlackToken(IMediator mediator, ILogger<SlackToken> logger, IEmailSender emailSender) : Endpoint<SlackTokenRequest, Results<Ok<string>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("slacktoken");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Receives a Slack token";
            s.Description = "Receives a Slack token";
            s.Responses[201] = "Token received successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest>> ExecuteAsync(SlackTokenRequest request, CancellationToken cancellationToken)
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

        await emailSender.SendEmailAsync(email, cancellationToken);

        var operationResult = Result.Success("ok");

        return TypedResultsBuilder
            .FromResult(operationResult)
            .SetTypedResults<Ok<string>, BadRequest>();
    }
}