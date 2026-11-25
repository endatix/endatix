using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Folders.GetBySlug;
using Endatix.Core.UseCases.Folders.List;
using Endatix.Core.UseCases.Folders.Update;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Folders;

public sealed class FolderStandingReadTests
{
    private readonly IRepository<Folder> _rows = Substitute.For<IRepository<Folder>>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly Folder _folder = new(1, "Original", "original", "ORIGINAL") { Id = 42 };

    [Theory]
    [InlineData(ResultStatus.Forbidden)]
    [InlineData(ResultStatus.Unavailable)]
    [InlineData(ResultStatus.Ok)]
    public async Task List_ReadsFolderListOnlyAfterAdmission(ResultStatus standing)
    {
        var ct = TestContext.Current.CancellationToken;
        _rows.ListAsync(Arg.Any<FolderSpecifications.ActiveFoldersSpec>(), ct).Returns([_folder]);
        var handler = new ListFoldersHandler(_rows, _tenant, Scope(standing, () => Assert.Empty(_rows.ReceivedCalls())));

        var result = await handler.Handle(new ListFoldersQuery(), ct);

        Assert.Equal(standing, result.Status);
        if (standing != ResultStatus.Ok)
        {
            Assert.Empty(_rows.ReceivedCalls());
            return;
        }
        Assert.Equal(_folder.Id, Assert.Single(result.Value).Id);
        await _rows.Received(1).ListAsync(Arg.Any<FolderSpecifications.ActiveFoldersSpec>(), ct);
    }

    [Theory]
    [InlineData(ResultStatus.Forbidden)]
    [InlineData(ResultStatus.Unavailable)]
    [InlineData(ResultStatus.Ok)]
    public async Task GetBySlug_ChecksExistenceBeforeStandingAndLoadsFolderOnlyAfterAdmission(ResultStatus standing)
    {
        var ct = TestContext.Current.CancellationToken;
        _rows.AnyAsync(Arg.Any<FolderSpecifications.FolderBySlugSpec>(), ct).Returns(true);
        _rows.SingleOrDefaultAsync(Arg.Any<FolderSpecifications.FolderBySlugSpec>(), ct).Returns(_folder);
        var handler = new GetFolderBySlugHandler(_rows, _tenant, Scope(standing, AssertOnlyVisibilityRead));

        var result = await handler.Handle(new GetFolderBySlugQuery("original"), ct);

        Assert.Equal(standing, result.Status);
        if (standing != ResultStatus.Ok)
        {
            AssertOnlyVisibilityRead();
            return;
        }
        Assert.Equal(_folder.Id, result.Value.Id);
        await _rows.Received(1).SingleOrDefaultAsync(Arg.Any<FolderSpecifications.FolderBySlugSpec>(), ct);
    }

    [Theory]
    [InlineData(ResultStatus.Forbidden)]
    [InlineData(ResultStatus.Unavailable)]
    [InlineData(ResultStatus.Ok)]
    public async Task Patch_ValidatesProjectedStateAndSlugBeforeStandingAndLoadsMutableFolderOnlyAfterAdmission(ResultStatus standing)
    {
        var ct = TestContext.Current.CancellationToken;
        _rows.FirstOrDefaultAsync(Arg.Any<FolderEditTarget.ById>(), ct).Returns(new FolderEditTarget(false, ""));
        _rows.FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), ct).Returns(_folder);
        var policy = new FolderWritePolicy(_rows, Substitute.For<IValueNormalizer>(), Scope(standing, AssertOnlyEditChecks));
        var handler = new UpdateFolderHandler(_rows, Substitute.For<IMediator>(), policy, Substitute.For<IUniqueConstraintViolationChecker>());

        var result = await handler.Handle(new UpdateFolderCommand(_folder.Id) { Slug = "renamed" }, ct);

        Assert.Equal(standing, result.Status);
        if (standing != ResultStatus.Ok)
        {
            AssertOnlyEditChecks();
            Assert.Equal("original", _folder.UrlSlug);
            return;
        }
        Assert.Equal("renamed", result.Value.UrlSlug);
        await _rows.Received(1).FirstOrDefaultAsync(Arg.Any<FolderSpecifications.FolderByIdSpec>(), ct);
        await _rows.Received(1).UpdateAsync(_folder, ct);
    }

    private void AssertOnlyVisibilityRead()
    {
        var call = Assert.Single(_rows.ReceivedCalls());
        Assert.Equal(nameof(_rows.AnyAsync), call.GetMethodInfo().Name);
        Assert.IsType<FolderSpecifications.FolderBySlugSpec>(call.GetArguments()[0]);
    }

    private void AssertOnlyEditChecks() => Assert.Collection(_rows.ReceivedCalls(),
        projection =>
        {
            Assert.Equal(nameof(_rows.FirstOrDefaultAsync), projection.GetMethodInfo().Name);
            Assert.IsType<FolderEditTarget.ById>(projection.GetArguments()[0]);
        },
        validation =>
        {
            Assert.Equal(nameof(_rows.AnyAsync), validation.GetMethodInfo().Name);
            Assert.IsType<FolderSpecifications.FolderExistsBySlugSpec>(validation.GetArguments()[0]);
        });

    private ManagedContentScope Scope(ResultStatus status, Action beforeDecision)
    {
        _tenant.TenantId.Returns(1);
        var authorization = Substitute.For<ICurrentUserAuthorizationService>();
        authorization.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            beforeDecision();
            return Result.Success(new AuthorizationData
            {
                UserId = "owner",
                RequiredActionsUnavailable = status == ResultStatus.Unavailable,
                RequiredActions = status == ResultStatus.Forbidden
                    ? [new RequiredAction { Key = "setup", ResolvePath = "/setup", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking }]
                    : []
            });
        });
        return new ManagedContentScope(_tenant, new AccountStanding(authorization));
    }
}
