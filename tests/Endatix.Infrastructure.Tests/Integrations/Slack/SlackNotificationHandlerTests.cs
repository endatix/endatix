using System.Net;
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
        await _formRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
        AssertNotLogged(LogLevel.Warning);
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
        await _formRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
        AssertNotLogged(LogLevel.Error);
    }

    [Theory]
    [InlineData("""{"Active":true,"ChannelId":"c","EndatixHubBaseUrl":"https://hub"}""")]
    [InlineData("""{"Active":true,"Token":"t","EndatixHubBaseUrl":"https://hub"}""")]
    [InlineData("""{"Active":true,"Token":"t","ChannelId":"c"}""")]
    public async Task Handle_ActiveWithIncompleteConfig_LogsWarningAndSkipsWithoutThrowing(string slackSettingsJson)
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: slackSettingsJson));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Warning, "is missing");
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
        _httpClientFactory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError)));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        AssertLogged(LogLevel.Error, "Slack notification failed");
    }

    [Fact]
    public async Task Handle_ActiveAndComplete_PostsMessage()
    {
        // Arrange
        GivenTenantSettings(new TenantSettings(TenantId, slackSettingsJson: ActiveSlackSettingsJson));
        var handler = new StubHandler(HttpStatusCode.OK);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var sut = CreateSut();

        // Act
        await sut.Handle(CompletedEvent(), CancellationToken.None);

        // Assert
        Assert.Equal(1, handler.Calls);
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

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
