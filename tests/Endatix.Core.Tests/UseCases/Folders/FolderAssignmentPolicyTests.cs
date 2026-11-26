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
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_NoTenant_DeniesAssignmentBeforeLookup()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0L);

        // Act
        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        _folderRepository.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_SameFolder_AllowsNoOp()
    {
        var result = await _helper.EnsureFolderMoveValidAsync(10, 10, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        _folderRepository.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_CurrentFolderImmutable_ReturnsConflict()
    {
        var locked = new Folder(SampleData.TENANT_ID, "Locked", "locked", "LOCKED") { Id = 10, Immutable = true };
        _folderRepository
            .AnyAsync(Arg.Any<ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<Folder>>().IsSatisfiedBy(locked));

        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().ContainSingle(e => e.Contains("locked folders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_ClearFromMutableFolder_Allows()
    {
        var result = await _helper.EnsureFolderMoveValidAsync(10, null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_RequiredAssignmentAndClear_ReturnsError()
    {
        _tenantSettingsRepository
            .AnyAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _helper.EnsureFolderMoveValidAsync(10, null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("assign a folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderAssignmentValidAsync_RequiredAssignmentAndNullFolder_ReturnsError()
    {
        _tenantSettingsRepository
            .AnyAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _helper.EnsureFolderAssignmentValidAsync(null, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("assign a folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderAssignmentValidAsync_ForeignActiveFolder_DoesNotMatchTheOrdinaryTenantPredicate()
    {
        // Arrange
        var policy = new FolderAssignmentPolicy(_tenantSettingsRepository, _folderRepository, _tenantContext) { UseClientErrors = true };
        var foreign = new Folder(SampleData.TENANT_ID + 1, "Neighbor", "neighbor", "NEIGHBOR") { Id = 11, IsActive = true };
        _folderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Folder, bool?>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<Folder, bool?>>().IsSatisfiedBy(foreign) ? (bool?)true : null);

        // Act
        var result = await policy.EnsureFolderAssignmentValidAsync(foreign.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_TargetFolderInactive_ReturnsError()
    {
        var inactive = new Folder(SampleData.TENANT_ID, "Inactive", "inactive", "INACTIVE") { Id = 11, IsActive = false };
        _folderRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Folder, bool?>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<Folder, bool?>>().IsSatisfiedBy(inactive) ? (bool?)inactive.IsActive : null);

        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("not active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureFolderMoveValidAsync_TargetFolderMissing_ReturnsError()
    {
        var result = await _helper.EnsureFolderMoveValidAsync(10, 11, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().ContainSingle(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureAndApplyFolderMoveAsync_WhenValidationFails_DoesNotApplyMove()
    {
        _tenantSettingsRepository
            .AnyAsync(Arg.Any<ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(true);

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
