using Endatix.Api.Endpoints.Integrations;
using Endatix.Core.Features.Email;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Tests.Endpoints.Integrations;

public class SlackTokenTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly SlackToken _endpoint;

    public SlackTokenTests()
    {
        _endpoint = Factory.Create<SlackToken>(Substitute.For<ILogger<SlackToken>>(), _emailSender);
    }

    [Fact]
    public async Task ExecuteAsync_NoToken_SendsEmptyBodies()
    {
        // Arrange
        var request = new SlackTokenRequest();

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.Result.Should().BeOfType<Ok<string>>();
        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailWithBody>(email => email.HtmlBody == string.Empty && email.PlainTextBody == string.Empty),
            Arg.Any<CancellationToken>());
    }
}
