using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.Folders;

public class FolderAssignmentPolicyTests
{
    private readonly IRepository<TenantSettingsEntity> _tenantSettingsRepository;
    private readonly IRepository<Folder> _folderRepository;
    private readonly ITenantContext _tenantContext;
    private readonly FolderAssignmentPolicy _helper;

    public FolderAssignmentPolicyTests()
    {
        _tenantSettingsRepository = Substitute.For<IRepository<TenantSettingsEntity>>();
        _folderRepository = Substitute.For<IRepository<Folder>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _helper = new FolderAssignmentPolicy(_tenantSettingsRepository, _folderRepository, _tenantContext);

        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns((TenantSettingsEntity?)null);
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_SameFolder_AllowsNoOp()
    {
        var result = await _helper.EnsureFolderMoveValidAsync(10, 10, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        await _folderRepository.DidNotReceive()
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_CurrentFolderImmutable_ReturnsConflict()
    {
        var immutableFolder = new Folder(SampleData.TENANT_ID, "Immutable", "immutable", "IMMUTABLE")
        {
            Id = 10,
            Immutable = true,
        };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(immutableFolder);

        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().ContainSingle(e => e.Contains("locked folders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_ClearFromMutableFolder_Allows()
    {
        var mutableFolder = new Folder(SampleData.TENANT_ID, "Mutable", "mutable", "MUTABLE")
        {
            Id = 10,
            Immutable = false,
        };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(mutableFolder);

        var result = await _helper.EnsureFolderMoveValidAsync(10, null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_RequiredAssignmentAndClear_ReturnsError()
    {
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        settings.UpdateRequireFolderAssignment(true);
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _helper.EnsureFolderMoveValidAsync(10, null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("assign a folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderAssignmentValidAsync_RequiredAssignmentAndNullFolder_ReturnsError()
    {
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        settings.UpdateRequireFolderAssignment(true);
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _helper.EnsureFolderAssignmentValidAsync(null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("assign a folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_TargetFolderInactive_ReturnsError()
    {
        var currentFolder = new Folder(SampleData.TENANT_ID, "Current", "current", "CURRENT")
        {
            Id = 10,
            Immutable = false,
        };
        var inactiveTarget = new Folder(SampleData.TENANT_ID, "Target", "target", "TARGET")
        {
            Id = 11,
            IsActive = false,
        };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(currentFolder, inactiveTarget);

        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("not active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_TargetFolderMissing_ReturnsError()
    {
        var currentFolder = new Folder(SampleData.TENANT_ID, "Current", "current", "CURRENT")
        {
            Id = 10,
            Immutable = false,
        };

        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(currentFolder, (Folder?)null);

        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureAndApplyFolderMoveAsync_WhenValidationFails_DoesNotApplyMove()
    {
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        settings.UpdateRequireFolderAssignment(true);
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var wasCalled = false;
        var result = await _helper.EnsureAndApplyFolderMoveAsync(
            currentFolderId: 10,
            requestedFolderId: null,
            applyMove: _ =>
            {
                wasCalled = true;
                return true;
            },
            cannotMoveMessage: "cannot move",
            cancellationToken: CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        wasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureAndApplyFolderMoveAsync_WhenApplyFails_ReturnsError()
    {
        var currentFolder = new Folder(SampleData.TENANT_ID, "Current", "current", "CURRENT")
        {
            Id = 10,
            Immutable = false,
        };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(currentFolder);

        var result = await _helper.EnsureAndApplyFolderMoveAsync(
            currentFolderId: 10,
            requestedFolderId: null,
            applyMove: _ => false,
            cannotMoveMessage: "cannot move",
            cancellationToken: CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("cannot move", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureAndApplyFolderMoveAsync_WhenApplySucceeds_ReturnsOk()
    {
        var currentFolder = new Folder(SampleData.TENANT_ID, "Current", "current", "CURRENT")
        {
            Id = 10,
            Immutable = false,
        };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(currentFolder);

        var result = await _helper.EnsureAndApplyFolderMoveAsync(
            currentFolderId: 10,
            requestedFolderId: null,
            applyMove: _ => true,
            cannotMoveMessage: "cannot move",
            cancellationToken: CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
    }

    [Fact]
    public async Task EnsureAndApplyFolderMoveAsync_WhenFolderUnchanged_ReturnsInvalidAndDoesNotApply()
    {
        var applyCalled = false;

        var result = await _helper.EnsureAndApplyFolderMoveAsync(
            currentFolderId: 10,
            requestedFolderId: 10,
            applyMove: _ =>
            {
                applyCalled = true;
                return true;
            },
            cannotMoveMessage: "cannot move",
            cancellationToken: CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(error =>
            error.Identifier == "folderId" &&
            error.ErrorMessage.Contains("unchanged", StringComparison.OrdinalIgnoreCase));
        applyCalled.Should().BeFalse();
    }
}
