using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceObtainTokenTests
{
    private readonly IRepository<Submission> _repository;
    private readonly SubmissionOptions _options;
    private readonly SubmissionTokenService _sut;

    public SubmissionTokenServiceObtainTokenTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _options = new SubmissionOptions { TokenExpiryInHours = 24 };
        var optionsWrapper = Substitute.For<IOptions<SubmissionOptions>>();
        optionsWrapper.Value.Returns(_options);
        _sut = new SubmissionTokenService(_repository, optionsWrapper);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ObtainToken_InvalidSubmissionId_ThrowsArgumentException(long submissionId)
    {
        // Act
        var act = () => _sut.ObtainToken(submissionId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("submissionId", ErrorType.ZeroOrNegative));
    }

    [Fact]
    public async Task ObtainToken_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var submissionId = 1L;
        _repository.GetByIdAsync(submissionId).Returns((Submission)null!);

        // Act
        var result = await _sut.ObtainToken(submissionId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        // result.Error.Should().Be("Submission not found");
        result.Errors.Should().Contain("Submission not found");
    }

    [Fact]
    public async Task ObtainToken_NewToken_ReturnsSuccess()
    {
        // Arrange
        var submissionId = 1L;
        var submission = new Submission { Id = submissionId };
        _repository.GetByIdAsync(submissionId).Returns(submission);

        // Act
        var result = await _sut.ObtainToken(submissionId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        await _repository.Received(1).SaveChangesAsync();
    }
}
