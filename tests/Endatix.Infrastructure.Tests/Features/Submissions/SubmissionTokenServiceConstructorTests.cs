using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceConstructorTests
{
    private readonly SubmissionOptions _options;

    public SubmissionTokenServiceConstructorTests()
    {
        _options = new SubmissionOptions { TokenExpiryInHours = 24 };
        var optionsWrapper = Substitute.For<IOptions<SubmissionOptions>>();
        optionsWrapper.Value.Returns(_options);
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Substitute.For<IOptions<SubmissionOptions>>();
        options.Value.Returns(new SubmissionOptions { TokenExpiryInHours = 24 });

        // Act
        var act = () => new SubmissionTokenService(null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("repository", ErrorType.Null));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = Substitute.For<IRepository<Submission>>();
        var options = Substitute.For<IOptions<SubmissionOptions>>();
        options.Value.Returns((SubmissionOptions)null!);

        // Act
        var act = () => new SubmissionTokenService(repository, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("options.Value", ErrorType.Null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidTokenExpiry_ThrowsArgumentException(int expiryHours)
    {
        // Arrange
        var repository = Substitute.For<IRepository<Submission>>();
        var options = Substitute.For<IOptions<SubmissionOptions>>();
        options.Value.Returns(new SubmissionOptions { TokenExpiryInHours = expiryHours });

        // Act
        var act = () => new SubmissionTokenService(repository, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("options.Value.TokenExpiryInHours", ErrorType.ZeroOrNegative));
    }
}
