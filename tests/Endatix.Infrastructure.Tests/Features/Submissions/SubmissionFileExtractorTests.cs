using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionFileExtractorTests
{
  private const long FormId = 1465276700186116096L;
  private const long SubmissionId = 1470691429209604096L;
  private const string StorageHost = "endatixstoragedev.blob.core.windows.net";
  private const string Container = "secure-vault";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<SubmissionFileExtractor> _logger;
  private readonly SubmissionFileUrlPolicy _urlPolicy;
  private readonly SubmissionFileExtractor _extractor;
  private DummyHandler? _handler;

  public SubmissionFileExtractorTests()
  {
    _httpClientFactory = Substitute.For<IHttpClientFactory>();
    _logger = Substitute.For<ILogger<SubmissionFileExtractor>>();
    _urlPolicy = CreatePolicy();
    _extractor = new SubmissionFileExtractor(_httpClientFactory, _urlPolicy, _logger);
  }

  private static SubmissionFileUrlPolicy CreatePolicy()
  {
    var options = Options.Create(new AzureBlobStorageProviderOptions
    {
      HostName = StorageHost,
      UserFilesContainerName = Container,
    });

    return new SubmissionFileUrlPolicy(options);
  }

  private static string CanonicalUrl(string fileName) =>
      $"https://{StorageHost}/{Container}/s/{FormId}/{SubmissionId}/{fileName}";

  private static string LegacyFlatBlobUrl(string fileName = "1381655571790299137.png") =>
      $"https://{StorageHost}/{Container}/{fileName}";

  private void ConfigureHttpClient(long? contentLength = null)
  {
    _handler = new DummyHandler(contentLength);
    var httpClient = new HttpClient(_handler);
    _httpClientFactory
        .CreateClient(SubmissionFileFetchHttpClient.Name)
        .Returns(httpClient);
  }

  [Fact]
  public async Task ExtractFiles_Base64Content_Works()
  {
    // Arrange
    var json = "{" +
        "\"fileQuestion\": {" +
        "\"name\": \"test.txt\"," +
        "\"type\": \"text/plain\"," +
        "\"content\": \"SGVsbG8gd29ybGQ=\"}" +
        "}";
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Single(files);
    var file = files[0];
    Assert.Equal("fileQuestion.txt", file.FileName);
    Assert.Equal("text/plain", file.MimeType);
    using var reader = new StreamReader(file.Content);
    Assert.Equal("Hello world", reader.ReadToEnd());
  }

  [Fact]
  public async Task ExtractFiles_DataUrlContent_Works()
  {
    // Arrange
    var json = "{" +
        "\"fileQuestion\": {" +
        "\"name\": \"test.txt\"," +
        "\"type\": \"text/plain\"," +
        "\"content\": \"data:text/plain;base64,SGVsbG8gd29ybGQ=\"}" +
        "}";
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Single(files);
    var file = files[0];
    Assert.Equal("fileQuestion.txt", file.FileName);
    Assert.Equal("text/plain", file.MimeType);
    using var reader = new StreamReader(file.Content);
    Assert.Equal("Hello world", reader.ReadToEnd());
  }

  [Fact]
  public async Task ExtractFiles_OversizedContentLength_IsSkipped()
  {
    const long maxBytes = 10 * 1024 * 1024;
    ConfigureHttpClient(contentLength: maxBytes + 1);
    var json = $$"""
        {
          "imagesUpload": [{
            "name": "foo.png",
            "type": "image/jpeg",
            "content": "{{CanonicalUrl("foo.png")}}"
          }]
        }
        """;
    var doc = JsonDocument.Parse(json);

    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Empty(files);
    Assert.True(_handler!.WasCalled);
  }

  [Fact]
  public async Task ExtractFiles_CanonicalUrlContent_Works()
  {
    // Arrange
    ConfigureHttpClient();
    var json = $$"""
        {
          "imagesUpload": [{
            "name": "foo.png",
            "type": "image/jpeg",
            "content": "{{CanonicalUrl("foo.png")}}"
          }]
        }
        """;
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Single(files);
    Assert.Equal("imagesUpload.png", files[0].FileName);
    Assert.True(_handler!.WasCalled);
  }

  [Fact]
  public async Task ExtractFiles_MaliciousUrl_DoesNotInvokeHttpClient()
  {
    // Arrange
    ConfigureHttpClient();
    var json = $$"""
        {
          "imagesUpload": [{
            "name": "foo.png",
            "type": "image/jpeg",
            "content": "http://169.254.169.254/latest/meta-data/"
          }]
        }
        """;
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(files);
    Assert.False(_handler!.WasCalled);
  }

  [Fact]
  public async Task ExtractFiles_LegacyFlatBlobUrl_IsSkipped()
  {
    // Arrange
    ConfigureHttpClient();
    var json = $$"""
        {
          "imagesUpload": [{
            "name": "foo.png",
            "type": "image/jpeg",
            "content": "{{LegacyFlatBlobUrl()}}"
          }]
        }
        """;
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(files);
    Assert.False(_handler!.WasCalled);
  }

  [Fact]
  public async Task ExtractFiles_MultipleFilesWithIndexing_Works()
  {
    // Arrange
    ConfigureHttpClient();
    var json = $$"""
        {
          "imagesUpload": [
            {
              "name": "foo.png",
              "type": "image/jpeg",
              "content": "{{CanonicalUrl("foo.png")}}"
            },
            {
              "name": "bar.png",
              "type": "image/jpeg",
              "content": "{{CanonicalUrl("bar.png")}}"
            }
          ]
        }
        """;
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, files.Count);
    Assert.Contains(files, f => f.FileName == "imagesUpload-1.png");
    Assert.Contains(files, f => f.FileName == "imagesUpload-2.png");
  }

  [Fact]
  public async Task ExtractFiles_NoFiles_ReturnsEmptyList()
  {
    // Arrange
    var json = """
        {
          "q1": "a1",
          "q2": "a2"
        }
        """;
    var doc = JsonDocument.Parse(json);

    // Act
    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(files);
  }

  [Fact]
  public async Task ExtractFiles_Prefix_Works()
  {
    var json = """
        {
          "fileQuestion": {
            "name": "test.txt",
            "type": "text/plain",
            "content": "SGVsbG8gd29ybGQ="
          }
        }
        """;
    var doc = JsonDocument.Parse(json);
    var prefix = "myprefix";

    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, prefix, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Single(files);
    Assert.Equal("myprefixfileQuestion.txt", files[0].FileName);
  }

  [Fact]
  public async Task ExtractFiles_CompositeQuestion_Works()
  {
    var json = """
        {
          "question9": {
            "audioUpload": [{
              "name": "audio.wav",
              "type": "audio/wav",
              "content": "SGVsbG8gd29ybGQ="
            }]
          }
        }
        """;
    var doc = JsonDocument.Parse(json);

    var files = await _extractor.ExtractFilesAsync(doc.RootElement, FormId, SubmissionId, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Single(files);
    Assert.Equal("question9.wav", files[0].FileName);
  }

  private sealed class DummyHandler(long? contentLength = null) : HttpMessageHandler
  {
    public bool WasCalled { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      WasCalled = true;
      var response = new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StreamContent(new MemoryStream([1, 2, 3])),
      };
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
      if (contentLength is not null)
      {
        response.Content.Headers.ContentLength = contentLength;
      }

      return Task.FromResult(response);
    }
  }
}
