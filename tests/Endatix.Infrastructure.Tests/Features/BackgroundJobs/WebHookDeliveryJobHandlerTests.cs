using System.Net;
using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.BackgroundJobs.Handlers;
using Endatix.Infrastructure.Features.WebHooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Features.BackgroundJobs;

public sealed class WebHookDeliveryJobHandlerTests : IDisposable
{
    private const long TenantId = 5;
    private const string EndpointUrl = "https://hooks.example.test/receive?token=secret";

    private readonly List<OutboxMessage> _messages = [];
    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly RecordingHttpHandler _http = new(HttpStatusCode.OK);
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IRepository<TenantSettings> _tenantSettings = Substitute.For<IRepository<TenantSettings>>();

    public WebHookDeliveryJobHandlerTests()
    {
        // Evaluates the handler's specification against the seeded rows, so the tenant scoping is exercised.
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());
        _tenantSettings
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new TenantSettings(TenantId, webHookSettingsJson: ConfigJson(EndpointUrl)));
    }

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task ExecuteAsync_Delivers_WithHookIdEqualToOutboxMessageId()
    {
        // Arrange
        var messageId = await SeedMessageAsync(TenantId);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(messageId, WebHookEndpointKey.Of(EndpointUrl)), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = _http.Requests.Should().ContainSingle().Subject;
        request.Headers.GetValues(WebHookRequestHeaders.HookId).Should().Equal(messageId.ToString());
        request.RequestUri.Should().Be(new Uri(EndpointUrl));
    }

    [Fact]
    public async Task ExecuteAsync_EndpointRemoved_ReturnsFailure()
    {
        // Arrange — the job was enqueued for an endpoint that has since been removed from the configuration.
        var messageId = await SeedMessageAsync(TenantId);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            Job(messageId, WebHookEndpointKey.Of("https://removed.example.test/")), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The webhook endpoint is no longer configured.");
        _http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_OutboxMessageOfOtherTenant_ReturnsFailure()
    {
        // Arrange — the job belongs to tenant 5, the message to tenant 6.
        var messageId = await SeedMessageAsync(tenantId: 6);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(messageId, WebHookEndpointKey.Of(EndpointUrl)), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The outbox message no longer exists.");
        _http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EndpointAnswersServerError_ThrowsWithoutUrl()
    {
        // Arrange
        var messageId = await SeedMessageAsync(TenantId);
        _http.Status = HttpStatusCode.ServiceUnavailable;
        var handler = CreateHandler();

        // Act
        var act = () => handler.ExecuteAsync(Job(messageId, WebHookEndpointKey.Of(EndpointUrl)), CancellationToken.None);

        // Assert — a retryable failure, and the URL's token never reaches the message.
        (await act.Should().ThrowAsync<WebHookDeliveryFailedException>())
            .Which.Message.Should().NotContain("secret");
    }

    [Fact]
    public async Task ExecuteAsync_EventDisabled_ReturnsFailure()
    {
        // Arrange
        var messageId = await SeedMessageAsync(TenantId);
        _tenantSettings
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new TenantSettings(TenantId, webHookSettingsJson: ConfigJson(EndpointUrl, isEnabled: false)));
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(messageId, WebHookEndpointKey.Of(EndpointUrl)), CancellationToken.None);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Webhook delivery for this event is disabled.");
        _http.Requests.Should().BeEmpty();
    }

    private WebHookDeliveryJobHandler CreateHandler()
    {
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(WebHookDeliveryJobHandler.HttpClientName).Returns(_ => new HttpClient(_http, disposeHandler: false));
        return new WebHookDeliveryJobHandler(
            _outbox,
            new WebHookEventConfigReader(_forms, _tenantSettings, NullLogger<WebHookEventConfigReader>.Instance),
            clients,
            NullLogger<WebHookServer>.Instance);
    }

    private Task<long> SeedMessageAsync(long tenantId)
    {
        var message = new OutboxMessage(
            "submission.completed",
            """{"formId":"12","submissionId":"34","tenantId":"5"}""",
            tenantId,
            DateTime.UtcNow,
            1);
        var id = 9_000L + _messages.Count;
        message.Id = id;
        _messages.Add(message);
        return Task.FromResult(id);
    }

    private static BackgroundJobContext Job(long messageId, string endpointKey) =>
        new(1, WebHookDeliveryPayload.JobType, TenantId,
            BackgroundJobPayloadSerializer.Serialize(new WebHookDeliveryPayload(messageId, endpointKey)), 1);

    private static string ConfigJson(string url, bool isEnabled = true) =>
        JsonSerializer.Serialize(new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new()
                {
                    IsEnabled = isEnabled,
                    WebHookEndpoints = [new WebHookEndpointConfig { Url = url }],
                },
            },
        });

    private sealed class RecordingHttpHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = status;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }
}

public sealed class WebHookJobRegistrationTests
{
    [Fact]
    public void AddWebHookProcessing_Default_KeepsLegacyClientRetryAttempts()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act
        services.AddWebHookProcessing();
        using var provider = services.BuildServiceProvider();

        // Assert — the client the relay and the legacy queue deliver through keeps its own retries; only webhook
        // jobs use the client without them.
        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebHookSettings>>().Value;
        settings.ServerSettings.RetryAttempts.Should().Be(5);
        provider.GetRequiredService<WebHookServer>().Should().NotBeNull();
        provider.GetRequiredService<IHttpClientFactory>().CreateClient(WebHookDeliveryJobHandler.HttpClientName)
            .Should().NotBeNull();
    }
}
