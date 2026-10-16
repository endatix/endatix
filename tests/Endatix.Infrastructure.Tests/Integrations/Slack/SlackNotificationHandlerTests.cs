using System.Net;
using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Integrations.Slack;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Integrations.Slack;

public class SlackNotificationHandlerTests
{
    private const long TenantId = 1;
    private const string PostUrl = "https://slack.com/api/chat.postMessage";

    private readonly ILogger<SlackNotificationHandler> _logger = Substitute.For<ILogger<SlackNotificationHandler>>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IRepository<TenantSettings> _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
    private readonly IRepository<Form> _formRepository = Substitute.For<IRepository<Form>>();

    [Fact]
    public async Task Handle_MissingTenantSettings_SkipsNotificationWithoutThrowing()
    {
        // Arrange
        GivenTenantSettings(null);
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        await _formRepository.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<ISpecification<Form, string>>(), Arg.Any<CancellationToken>());
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
        AssertLogged(LogLevel.Warning, "no settings row");
        AssertNotLogged(LogLevel.Error);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Active":null}""")]
    [InlineData("""{"Active":false,"Token":"t","ChannelId":"c","EndatixHubBaseUrl":"https://hub"}""")]
    public async Task Handle_SlackNotActive_SkipsNotificationWithoutThrowing(string slackSettingsJson)
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: slackSettingsJson));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        await _formRepository.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<ISpecification<Form, string>>(), Arg.Any<CancellationToken>());
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
        AssertNotLogged(LogLevel.Error);
    }

    [Theory]
    [InlineData("""{"Active":true,"ChannelId":"c","EndatixHubBaseUrl":"https://hub"}""")]
    [InlineData("""{"Active":true,"Token":"t","EndatixHubBaseUrl":"https://hub"}""")]
    [InlineData("""{"Active":true,"Token":"t","ChannelId":"c"}""")]
    [InlineData("""{"Active":true,"Token":" ","ChannelId":"c","EndatixHubBaseUrl":"https://hub"}""")]
    public async Task Handle_ActiveWithIncompleteConfig_LogsWarningAndSkipsWithoutThrowing(string slackSettingsJson)
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: slackSettingsJson));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Warning, "setting is missing");
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_MalformedSlackSettingsJson_LogsErrorWithoutThrowing()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: "{not json"));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Error, "Slack notification failed");
    }

    [Fact]
    public async Task Handle_SlackApiFails_LogsErrorWithoutThrowing()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: ActiveSlackSettingsJson));
        _httpClientFactory.CreateClient(SlackNotificationHandler.HttpClientName)
            .Returns(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError)));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Error, "Slack notification failed");
    }

    [Fact]
    public async Task Handle_SlackOkFalse_LogsTheErrorWithoutThrowing()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: ActiveSlackSettingsJson));
        _httpClientFactory.CreateClient(SlackNotificationHandler.HttpClientName)
            .Returns(new HttpClient(new StubHandler(HttpStatusCode.OK, """{"ok":false,"error":"token_revoked"}""")));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Error, "token_revoked");
        AssertNotLogged(LogLevel.Information);
    }

    [Fact]
    public async Task Handle_SlackTimesOut_LogsErrorWithoutThrowing()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: ActiveSlackSettingsJson));
        _httpClientFactory.CreateClient(SlackNotificationHandler.HttpClientName)
            .Returns(new HttpClient(new StubHandler(new TaskCanceledException())));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Error, "Slack notification failed");
    }

    [Fact]
    public async Task Handle_RequestCancelled_Rethrows()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TenantSettings?>(new OperationCanceledException(cancelled.Token)));
        var sut = CreateSut();

        // Act
        var act = () => sut.Handle(CompletedEvent(), cancelled.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Handle_ActiveAndComplete_PostsMessage()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: ActiveSlackSettingsJson));
        GivenFormName("Q&A <draft>");
        var handler = new StubHandler(HttpStatusCode.OK, """{"ok":true}""");
        _httpClientFactory.CreateClient(SlackNotificationHandler.HttpClientName).Returns(new HttpClient(handler));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        Assert.Equal(1, handler.Calls);
        Assert.Equal(PostUrl, handler.Url);
        Assert.Equal("Bearer t", handler.Authorization);
        using var body = JsonDocument.Parse(handler.Content!);
        Assert.Equal("c", body.RootElement.GetProperty("channel").GetString());
        Assert.Equal(
            ":page_with_curl: <https://hub/forms/1/submissions/0|New submission> for Q&amp;A &lt;draft&gt;",
            body.RootElement.GetProperty("text").GetString());
        AssertNotLogged(LogLevel.Error);
    }

    private const string ActiveSlackSettingsJson =
        """{"Active":true,"Token":"t","ChannelId":"c","EndatixHubBaseUrl":"https://hub"}""";

    private SlackNotificationHandler CreateSut() =>
        new(_logger, _httpClientFactory, _tenantSettingsRepository, _formRepository);

    private void GivenTenantSettings(TenantSettings? settings) =>
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

    private void GivenFormName(string name) =>
        _formRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Form, string>>(), Arg.Any<CancellationToken>())
            .Returns(name);

    private static SubmissionCompletedEvent CompletedEvent() =>
        new(Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: 1,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true)));

    private void AssertLogged(LogLevel level, string text) =>
        _logger.Received().Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(text)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

    private void AssertNotLogged(LogLevel level) =>
        _logger.DidNotReceive().Log(
            level,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode statusCode, string body = """{"ok":true}""")
        {
            _statusCode = statusCode;
            _body = body;
        }

        public StubHandler(Exception exception) => _exception = exception;

        public int Calls { get; private set; }

        public string? Url { get; private set; }

        public string? Authorization { get; private set; }

        public string? Content { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Url = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            if (request.Content is not null)
            {
                Content = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            return new HttpResponseMessage(_statusCode) { Content = new StringContent(_body) };
        }
    }
}
