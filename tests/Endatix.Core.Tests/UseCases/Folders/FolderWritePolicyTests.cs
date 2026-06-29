using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.Tests.UseCases.Folders;

/// <summary>
/// Tests for the <see cref="FolderWritePolicy"/> class.
/// </summary>
public sealed class FolderWritePolicyTests
{
    private readonly IRepository<Folder> _repository;
    private readonly IValueNormalizer _valueNormalizer;
    private readonly FolderWritePolicy _policy;

    public FolderWritePolicyTests()
    {
        _repository = Substitute.For<IRepository<Folder>>();
        _valueNormalizer = Substitute.For<IValueNormalizer>();
        _policy = new FolderWritePolicy(_repository, _valueNormalizer);
    }

    [Fact]
    public void NormalizeNameOrError_WhenNormalizerReturnsNull_ReturnsError()
    {
        _valueNormalizer.Normalize("Name").Returns((string?)null);

        var result = _policy.NormalizeNameOrError("Name");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void NormalizeAndValidateSlugOrError_WhenSlugReserved_ReturnsError()
    {
        var result = _policy.NormalizeAndValidateSlugOrError("templates", "Fallback", includeDetailedInvalidMessage: false);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task NormalizedNameExistsAsync_DelegatesToRepository()
    {
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var exists = await FolderWritePolicy.NormalizedNameExistsAsync(_repository, "NAME", 3, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public void CreateDuplicateNameValidationError_ReturnsExpectedValidationError()
    {
        var error = _policy.CreateDuplicateNameValidationError("My Folder", "Name");

        error.Identifier.Should().Be("Name");
        error.ErrorMessage.Should().Contain("My Folder");
    }
}
