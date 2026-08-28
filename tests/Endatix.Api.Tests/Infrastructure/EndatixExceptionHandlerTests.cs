using System.Text.Json;
using Endatix.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using static Endatix.Api.Infrastructure.ResultExtensions;

namespace Endatix.Api.Tests.Infrastructure;

public class EndatixExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WritesGeneric500ProblemJson_WithoutExceptionMessage()
    {
        // Arrange
        const string secretMessage = "Secret DB connection string leaked";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/forms";
        httpContext.TraceIdentifier = "trace-exception";
        httpContext.Response.Body = new MemoryStream();

        var handler = new EndatixExceptionHandler(NullLogger<EndatixExceptionHandler>.Instance);
        var exception = new InvalidOperationException(secretMessage);

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        JsonElement root = document.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("title").GetString().Should().Be(ResultTitles.INTERNAL_SERVER_ERROR);
        root.GetProperty("detail").GetString().Should().Be(ResultTitles.INTERNAL_SERVER_ERROR);
        root.GetProperty("detail").GetString().Should().NotContain(secretMessage);
        root.GetProperty("instance").GetString().Should().Be("/api/forms");
        root.GetProperty("traceId").GetString().Should().Be("trace-exception");
    }

    [Fact]
    public async Task TryHandleAsync_WhenResponseHasStarted_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature());

        var handler = new EndatixExceptionHandler(NullLogger<EndatixExceptionHandler>.Instance);

        // Act
        bool handled = await handler.TryHandleAsync(
            httpContext,
            new Exception("after start"),
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeFalse();
    }

    private sealed class StartedHttpResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
