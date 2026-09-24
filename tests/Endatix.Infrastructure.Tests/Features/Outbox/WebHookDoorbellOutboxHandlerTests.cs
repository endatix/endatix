using System.Net;
using System.Net.Sockets;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public class WebHookDoorbellOutboxHandlerTests
{
    private const string WorkerKey = "doorbell-test-worker-key-0123456789abcdef";

    [Fact]
    public async Task HandleAsync_WebHookEvent_PostsIdOnlyRequestWithKey()
    {
        // Arrange
        var worker = new StubWorker(HttpStatusCode.Accepted);
        var sut = CreateSut(worker);

        // Act
        await sut.HandleAsync(Message(id: 555, tenantId: 7), CancellationToken.None);

        // Assert
        var request = worker.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Path.Should().Be("/webhook-deliveries");
        request.WorkerKey.Should().Be(WorkerKey);
        request.Body.Should().Be("""{"tenantId":"7","outboxMessageId":"555","eventType":"form.created","formId":"42"}""");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task HandleAsync_WorkerAccepts_Returns(HttpStatusCode status)
    {
        // Arrange
        var sut = CreateSut(new StubWorker(status));

        // Act
        var act = () => sut.HandleAsync(Message(id: 555, tenantId: 7), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_WorkerAnswers503_Throws()
    {
        // Arrange
        var sut = CreateSut(new StubWorker(HttpStatusCode.ServiceUnavailable));

        // Act
        var act = () => sut.HandleAsync(Message(id: 555, tenantId: 7), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("*503*");
    }

    [Fact]
    public async Task HandleAsync_ZeroTenant_ThrowsWithoutRequest()
    {
        // Arrange
        var worker = new StubWorker(HttpStatusCode.Accepted);
        var sut = CreateSut(worker);

        // Act
        var act = () => sut.HandleAsync(Message(id: 555, tenantId: 0), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        worker.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WorkerUnreachable_Throws()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var unusedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{unusedPort}"), Timeout = TimeSpan.FromSeconds(2) };
        var sut = CreateSut(client);

        // Act
        var act = () => sut.HandleAsync(Message(id: 555, tenantId: 7), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static WebHookDoorbellOutboxHandler CreateSut(StubWorker worker) =>
        CreateSut(new HttpClient(worker) { BaseAddress = new Uri("http://worker.test") });

    private static WebHookDoorbellOutboxHandler CreateSut(HttpClient client)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WebHookDoorbellOutboxHandler.HttpClientName).Returns(client);
        var options = Options.Create(new WebHookDoorbellOptions
        {
            Enabled = true,
            WorkerBaseUrl = client.BaseAddress!.ToString(),
            WorkerApiKey = WorkerKey,
        });
        return new WebHookDoorbellOutboxHandler(factory, options, NullLogger<WebHookDoorbellOutboxHandler>.Instance);
    }

    private static IOutboxMessage Message(long id, long tenantId) =>
        new FakeOutboxMessage(id, "form.created", """{"formId":"42","tenantId":"7","name":"Datanium intake"}""", tenantId);

    private sealed record FakeOutboxMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string? WorkerKey, string? Body);

    private sealed class StubWorker(HttpStatusCode status) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues(WebHookDoorbellOutboxHandler.WorkerKeyHeader, out var key) ? key.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status);
        }
    }
}
