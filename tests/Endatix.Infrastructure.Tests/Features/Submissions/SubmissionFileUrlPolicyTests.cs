using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionFileUrlPolicyTests
{
    private const long FormId = 1465276700186116096L;
    private const long SubmissionId = 1470691429209604096L;
    private const string Host = "storage.example.com";
    private const string Container = "secure-vault";

    private static SubmissionFileUrlPolicy CreatePolicy(
        string? host = Host,
        string container = Container,
        int? port = null)
    {
        var options = Options.Create(new AzureBlobStorageProviderOptions
        {
            HostName = host ?? string.Empty,
            UserFilesContainerName = container,
            Port = port,
        });

        return new SubmissionFileUrlPolicy(options);
    }

    private static string CanonicalUrl(string fileName = "file.png", string? host = null, int? port = null)
    {
        var resolvedHost = host ?? Host;
        var authority = port is null ? resolvedHost : $"{resolvedHost}:{port}";
        return $"https://{authority}/{Container}/s/{FormId}/{SubmissionId}/{fileName}";
    }

    [Fact]
    public void TryParseCanonicalPath_AllowsMatchingCanonicalUrl()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri(CanonicalUrl());

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out var fileName);

        // Assert
        Assert.True(allowed);
        Assert.Equal("file.png", fileName);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsMetadataIpHost()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri(
            $"http://169.254.169.254/{Container}/s/{FormId}/{SubmissionId}/file.png");

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsLoopbackHost()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri($"http://127.0.0.1/{Container}/s/{FormId}/{SubmissionId}/file.png");

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsWrongSubmissionId()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri(CanonicalUrl().Replace(SubmissionId.ToString(), "999"));

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsMissingSubmissionSegment()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri($"https://{Host}/{Container}/x/{FormId}/{SubmissionId}/file.png");

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsParentDirectorySegment()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri($"https://{Host}/{Container}/s/{FormId}/{SubmissionId}/../other.png");

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsUnconfiguredNonDefaultPort()
    {
        var policy = CreatePolicy();
        var uri = new Uri($"https://{Host}:8080/{Container}/s/{FormId}/{SubmissionId}/file.png");

        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        Assert.False(allowed);
    }

    [Fact]
    public void TryValidateForFetch_RejectsUnconfiguredNonDefaultPort()
    {
        var policy = CreatePolicy();

        var allowed = policy.TryValidateForFetch(
            $"https://{Host}:8080/{Container}/s/{FormId}/{SubmissionId}/file.png",
            FormId,
            SubmissionId);

        Assert.False(allowed);
    }

    [Fact]
    public void TryValidateForFetch_AllowsConfiguredDevPort()
    {
        const string devHost = "127.0.0.1";
        const int devPort = 10000;
        var policy = CreatePolicy(host: devHost, port: devPort);

        var allowed = policy.TryValidateForFetch(
            $"http://{devHost}:{devPort}/{Container}/s/{FormId}/{SubmissionId}/file.png",
            FormId,
            SubmissionId);

        Assert.True(allowed);
    }

    [Fact]
    public void TryValidateForFetch_RejectsWrongPortWhenPortConfigured()
    {
        const string devHost = "localhost";
        var policy = CreatePolicy(host: devHost, port: 9000);

        var allowed = policy.TryValidateForFetch(
            $"http://{devHost}:8080/{Container}/s/{FormId}/{SubmissionId}/file.png",
            FormId,
            SubmissionId);

        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsHostNotInAllowList()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri(CanonicalUrl().Replace(Host, "evil.example.com"));

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsUserInfoInUri()
    {
        // Arrange
        var policy = CreatePolicy();
        var uri = new Uri($"https://user@{Host}/{Container}/s/{FormId}/{SubmissionId}/file.png");

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryParseCanonicalPath_RejectsWhenStorageNotConfigured()
    {
        // Arrange
        var policy = CreatePolicy(host: string.Empty);
        var uri = new Uri(CanonicalUrl());

        // Act
        var allowed = policy.TryParseCanonicalPath(uri, FormId, SubmissionId, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TryValidateForFetch_AllowsMatchingCanonicalUrl()
    {
        // Arrange
        var policy = CreatePolicy();

        // Act
        var allowed = policy.TryValidateForFetch(CanonicalUrl(), FormId, SubmissionId);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public void TryValidateForFetch_RejectsNonCanonicalUrl()
    {
        // Arrange
        var policy = CreatePolicy();

        // Act
        var allowed = policy.TryValidateForFetch(
            $"https://{Host}/{Container}/other/path/file.png",
            FormId,
            SubmissionId);

        // Assert
        Assert.False(allowed);
    }
}
