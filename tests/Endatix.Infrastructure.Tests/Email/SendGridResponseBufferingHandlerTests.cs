using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Email;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Endatix.Infrastructure.Tests.Email;

public class SendGridResponseBufferingHandlerTests
{
    private static readonly SendGridClientOptions _clientOptions = new() { ApiKey = "SG.test-api-key" };

    [Fact]
    public async Task SdkWithoutHandler_CancelledAfterProviderAccepted_Throws()
    {
        using var cts = new CancellationTokenSource();
        var client = new SendGridClient(new HttpClient(new AcceptThenCancelHttpMessageHandler(cts)), _clientOptions);

        Func<Task> act = () => client.SendEmailAsync(CreateMessage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendEmail_CancelledAfterProviderAccepted_ReturnsProviderResponse()
    {
        using var cts = new CancellationTokenSource();
        var client = CreateClient(new AcceptThenCancelHttpMessageHandler(cts));

        var response = await client.SendEmailAsync(CreateMessage(), cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Body.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("{}");
    }

    [Fact]
    public async Task SendEmail_ResponseBodyFailsAfterProviderAccepted_ReturnsProviderStatus()
    {
        using var cts = new CancellationTokenSource();
        var client = CreateClient(new AcceptThenCancelHttpMessageHandler(cts, resetOnRead: true));

        var response = await client.SendEmailAsync(CreateMessage(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task SendEmail_CancelledBeforeProviderAnswers_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var client = CreateClient(new AcceptThenCancelHttpMessageHandler(cts));

        Func<Task> act = () => client.SendEmailAsync(CreateMessage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RegisteredClient_CancelledAfterProviderAccepted_DoesNotFail()
    {
        using var cts = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.Configure<SendGridSettings>(settings => settings.ApiKey = "SG.test-api-key");
        SendGridEmailSender.InitializationDelegate(services);
        services.AddHttpClient("InjectableSendGridClient")
            .ConfigurePrimaryHttpMessageHandler(() => new AcceptThenCancelHttpMessageHandler(cts));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ISendGridClient>();

        var response = await client.SendEmailAsync(CreateMessage(), cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private static SendGridClient CreateClient(HttpMessageHandler provider) =>
        new(new HttpClient(new SendGridResponseBufferingHandler { InnerHandler = provider }), _clientOptions);

    private static SendGridMessage CreateMessage() => MailHelper.CreateSingleEmail(
        new EmailAddress("sender@example.com"),
        new EmailAddress("recipient@example.com"),
        "Test Subject",
        "Hello World",
        "<html>Hello World</html>");

    private sealed class AcceptThenCancelHttpMessageHandler(CancellationTokenSource cts, bool resetOnRead = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            cts.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new CancellationAwareContent("{}", resetOnRead)
            });
        }
    }
}
