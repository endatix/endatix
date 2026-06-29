using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.Update;
using Endatix.Core.UseCases.Folders;
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
}
