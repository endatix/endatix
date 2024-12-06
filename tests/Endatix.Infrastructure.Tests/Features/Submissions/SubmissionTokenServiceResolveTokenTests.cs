using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceResolveTokenTests
{
    private readonly IRepository<Submission> _repository;
    private readonly SubmissionOptions _options;
    private readonly SubmissionTokenService _sut;

    public SubmissionTokenServiceResolveTokenTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _options = new SubmissionOptions { TokenExpiryInHours = 24 };
        var optionsWrapper = Substitute.For<IOptions<SubmissionOptions>>();
        optionsWrapper.Value.Returns(_options);
        _sut = new SubmissionTokenService(_repository, optionsWrapper);
    }

    [Fact]
    public async Task ResolveToken_NullOrEmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var token = string.Empty;

        // Act
        var act = () => _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Empty));
    }

    [Fact]
    public async Task ResolveToken_TokenNotFound_ReturnsNotFound()
    {
        // Arrange
        var token = "invalid-token";
        _repository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns((Submission)null!);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired token");
    }

    [Fact]
    public async Task ResolveToken_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var token = "valid-token";
        var submissionId = 1L;
        var submission = new Submission(SampleData.FORM_DEFINITION_JSON_DATA_1, 2, 3, false) { Id = submissionId };
        submission.UpdateToken(new Token(24));
        _repository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns(submission);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submissionId);
    }

    [Fact]
    public async Task ResolveToken_WhenSubmissionIsComplete_ReturnsNotFound()
    {
        // Arrange
        var token = "valid-token";
        var submission = new Submission("{ }", 123, isComplete: true) { Token = new Token(24) };
        _repository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns(submission);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Submission completed");
    }
}
