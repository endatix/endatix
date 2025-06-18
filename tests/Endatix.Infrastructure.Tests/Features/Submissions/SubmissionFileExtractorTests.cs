using System.Text.Json;
using Endatix.Infrastructure.Features.Submissions;
using System.Net;
using System.Net.Http.Headers;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionFileExtractorTests
{
    [Fact]
    public void ExtractFiles_Base64Content_Works()
    {
        // Arrange
        var json = "{" +
            "\"fileQuestion\": {" +
            "\"name\": \"test.txt\"," +
            "\"type\": \"text/plain\"," +
            "\"content\": \"SGVsbG8gd29ybGQ=\"}" +
            "}";
        var doc = JsonDocument.Parse(json);
        var extractor = new SubmissionFileExtractor(new HttpClient());

        // Act
        var files = extractor.ExtractFiles(doc.RootElement);

        // Assert
        Assert.Single(files);
        var file = files[0];
        Assert.Equal("fileQuestion.txt", file.FileName);
        Assert.Equal("text/plain", file.MimeType);
        using var reader = new StreamReader(file.Content);
        Assert.Equal("Hello world", reader.ReadToEnd());
    }

    [Fact]
    public void ExtractFiles_DataUrlContent_Works()
    {
        // Arrange
        var json = "{" +
            "\"fileQuestion\": {" +
            "\"name\": \"test.txt\"," +
            "\"type\": \"text/plain\"," +
            "\"content\": \"data:text/plain;base64,SGVsbG8gd29ybGQ=\"}" +
            "}";
        var doc = JsonDocument.Parse(json);
        var extractor = new SubmissionFileExtractor(new HttpClient());

        // Act
        var files = extractor.ExtractFiles(doc.RootElement);

        // Assert
        Assert.Single(files);
        var file = files[0];
        Assert.Equal("fileQuestion.txt", file.FileName);
        Assert.Equal("text/plain", file.MimeType);
        using var reader = new StreamReader(file.Content);
        Assert.Equal("Hello world", reader.ReadToEnd());
    }

    private class DummyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3 }))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void ExtractFiles_MultipleFilesWithIndexing_Works()
    {
        // Arrange
        var json = @"{
  ""question4"": ""Item 2"",
  ""question5"": ""Item 1"",
  ""imagesUpload"": [
    {
      ""name"": ""foo.png"",
      ""type"": ""image/jpeg"",
      ""content"": ""https://endatixstorage.blob.core.windows.net/1381655571790299137.png""
    },
    {
      ""name"": ""bar.png"",
      ""type"": ""image/jpeg"",
      ""content"": ""https://endatixstorage.blob.core.windows.net/1381655571790299138.png""
    }
  ]
}";
        var doc = JsonDocument.Parse(json);

        var httpClient = new HttpClient(new DummyHandler());
        var extractor = new SubmissionFileExtractor(httpClient);

        // Act
        var files = extractor.ExtractFiles(doc.RootElement);

        // Assert
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FileName == "imagesUpload-1.png");
        Assert.Contains(files, f => f.FileName == "imagesUpload-2.png");
        foreach (var file in files)
        {
            Assert.Equal("image/jpeg", file.MimeType);
            using var ms = new MemoryStream();
            file.Content.CopyTo(ms);
            Assert.Equal(new byte[] { 1, 2, 3 }, ms.ToArray());
        }
    }

    [Fact]
    public void ExtractFiles_NoFiles_ReturnsEmptyList()
    {
        // Arrange
        var json = @"{
  ""q1"": ""a1"",
  ""q2"": ""a2"",
  ""q3"": ""a3""
}";
        var doc = JsonDocument.Parse(json);
        var extractor = new SubmissionFileExtractor(new HttpClient());

        // Act
        var files = extractor.ExtractFiles(doc.RootElement);

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public void ExtractFiles_Prefix_Works()
    {
        // Arrange
        var json = @"{
  ""fileQuestion"": {
    ""name"": ""test.txt"",
    ""type"": ""text/plain"",
    ""content"": ""SGVsbG8gd29ybGQ=""}
}";
        var doc = JsonDocument.Parse(json);
        var extractor = new SubmissionFileExtractor(new HttpClient());
        var prefix = "myprefix";

        // Act
        var files = extractor.ExtractFiles(doc.RootElement, prefix);

        // Assert
        Assert.Single(files);
        var file = files[0];
        Assert.Equal("myprefix-fileQuestion.txt", file.FileName);
        Assert.Equal("text/plain", file.MimeType);
        using var reader = new StreamReader(file.Content);
        Assert.Equal("Hello world", reader.ReadToEnd());
    }

    [Fact]
    public void ExtractFiles_CompositeQuestion_Works()
    {
        // Arrange
        var json = @"{
  ""q1"": ""Item 2"",
  ""q2"": ""Item 1"",
  ""question9"": {
    ""audioUpload"": [
      {
    ""name"": ""audio.wav"",
    ""type"": ""audio/wav"",
    ""content"": ""SGVsbG8gd29ybGQ=""}
    ]
  }
}";
        var doc = JsonDocument.Parse(json);
        var extractor = new SubmissionFileExtractor(new HttpClient());

        // Act
        var files = extractor.ExtractFiles(doc.RootElement);

        // Assert
        Assert.Single(files);
        var file = files[0];
        Assert.Equal("question9.wav", file.FileName); // Should include composite question key
        Assert.Equal("audio/wav", file.MimeType);
        using var ms = new MemoryStream();
        file.Content.CopyTo(ms);
        Assert.Equal(Convert.FromBase64String("SGVsbG8gd29ybGQ="), ms.ToArray());
    }
}