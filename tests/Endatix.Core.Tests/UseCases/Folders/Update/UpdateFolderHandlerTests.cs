using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Folders.Update;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Folders.Update;

public sealed class UpdateFolderHandlerTests
{
    private readonly IRepository<Folder> _repository;
    private readonly UpdateFolderHandler _handler;
    private readonly IMediator _mediator;
    private readonly IValueNormalizer _valueNormalizer;
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker;
    private readonly FolderWritePolicy _folderWritePolicy;

    public UpdateFolderHandlerTests()
    {
        _repository = Substitute.For<IRepository<Folder>>();
        _mediator = Substitute.For<IMediator>();
        _valueNormalizer = Substitute.For<IValueNormalizer>();
        _uniqueConstraintViolationChecker = Substitute.For<IUniqueConstraintViolationChecker>();
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns(callInfo =>
        {
            var s = callInfo.Arg<string>();
            return string.IsNullOrEmpty(s) ? null : s.ToUpperInvariant();
        });

        _folderWritePolicy = new FolderWritePolicy(_repository, _valueNormalizer);
        _handler = new UpdateFolderHandler(_repository, _mediator, _folderWritePolicy, _uniqueConstraintViolationChecker);
    }

    [Fact]
    public async Task Handle_ImmutableFolder_RejectsNameChange()
    {
        var folder = new Folder(1L, "Locked", "locked", "LOCKED") { Id = 10L, Immutable = true };
        _repository.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(folder);

        var cmd = new UpdateFolderCommand(10L) { Name = "New" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.ErrorMessage.Contains("cannot be modified", StringComparison.OrdinalIgnoreCase));
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImmutableFolder_AllowsDeactivate()
    {
        var folder = new Folder(1L, "Locked", "locked", "LOCKED") { Id = 10L, Immutable = true, IsActive = true };
        _repository.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(folder);

        var cmd = new UpdateFolderCommand(10L) { IsActive = false };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        folder.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(folder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImmutableFolder_AllowsUnlockAndRenameInOneRequest()
    {
        var folder = new Folder(1L, "Locked", "locked", "LOCKED") { Id = 10L, Immutable = true };
        _repository.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(folder);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var cmd = new UpdateFolderCommand(10L) { Immutable = false, Name = "Unlocked" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        folder.Immutable.Should().BeFalse();
        folder.Name.Should().Be("Unlocked");
        folder.NormalizedName.Should().Be("UNLOCKED");
        await _repository.Received(1).UpdateAsync(folder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MutableFolder_CanSetImmutable()
    {
        var folder = new Folder(1L, "Open", "open", "OPEN") { Id = 11L, Immutable = false };
        _repository.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(folder);

        var cmd = new UpdateFolderCommand(11L) { Immutable = true };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        folder.Immutable.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(folder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameCannotBeNormalized_ReturnsInvalid()
    {
        LoadFolder();
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns((string?)null);

        var result = await _handler.Handle(new UpdateFolderCommand(11L) { Name = "???" }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error => error.ErrorMessage.Contains("normalized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsInvalid()
    {
        LoadFolder();
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(new UpdateFolderCommand(11L) { Name = "Taken" }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error => error.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_ReservedSlug_ReturnsInvalid()
    {
        LoadFolder();

        var result = await _handler.Handle(new UpdateFolderCommand(11L) { Slug = "api" }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error => error.Identifier == nameof(UpdateFolderCommand.Slug));
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ReturnsInvalid()
    {
        LoadFolder();
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(new UpdateFolderCommand(11L) { Slug = "taken-slug" }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error => error.ErrorMessage.Contains("taken-slug", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_ValidSlugAndText_UpdatesFolder()
    {
        var folder = LoadFolder();
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(
            new UpdateFolderCommand(11L) { Slug = "new-slug", Description = "  hello  ", Metadata = "{}" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        folder.UrlSlug.Should().Be("new-slug");
        folder.Description.Should().Be("hello");
        folder.Metadata.Should().Be("{}");
    }

    [Fact]
    public async Task Handle_NonUniqueSaveFailure_Rethrows()
    {
        LoadFolder();
        FailSave(new UniqueConstraintViolationResult(false, null, null));

        var act = () => _handler.Handle(new UpdateFolderCommand(11L) { IsActive = false }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(nameof(Folder.UrlSlug), "slug")]
    [InlineData(nameof(Folder.NormalizedName), "name")]
    [InlineData("other", "name")]
    public async Task Handle_UniqueSaveFailure_ReturnsInvalid(string column, string messagePart)
    {
        var folder = LoadFolder();
        FailSave(new UniqueConstraintViolationResult(true, null, column));

        var result = await _handler.Handle(new UpdateFolderCommand(11L) { IsActive = false }, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error => error.ErrorMessage.Contains(column == "other" ? folder.Name : messagePart, StringComparison.OrdinalIgnoreCase));
    }

    private Folder LoadFolder()
    {
        var folder = new Folder(1L, "Open", "open", "OPEN") { Id = 11L };
        _repository.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(folder);
        return folder;
    }

    private void FailSave(UniqueConstraintViolationResult violation)
    {
        _repository.UpdateAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("db")));
        _uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(Arg.Any<Exception>()).Returns(violation);
    }
}
