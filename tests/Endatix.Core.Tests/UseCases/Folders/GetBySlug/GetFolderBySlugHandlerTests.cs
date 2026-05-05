using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.GetBySlug;

namespace Endatix.Core.Tests.UseCases.Folders.GetBySlug;

public sealed class GetFolderBySlugHandlerTests
{
    private readonly IRepository<Folder> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly GetFolderBySlugHandler _handler;

    public GetFolderBySlugHandlerTests()
    {
        _repository = Substitute.For<IRepository<Folder>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(1L);
        _handler = new GetFolderBySlugHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_ValidSlug_ReturnsFolder()
    {
        var folder = new Folder(1L, "Test Folder", "test-folder") { Id = 1L };

        _repository.SingleOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderBySlugSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        var result = await _handler.Handle(new GetFolderBySlugQuery("test-folder"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test Folder");
    }

    [Fact]
    public async Task Handle_FolderNotFound_ReturnsNotFound()
    {
        _repository.SingleOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderBySlugSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((Folder?)null);

        var result = await _handler.Handle(new GetFolderBySlugQuery("nonexistent"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidSlug_CallsRepository()
    {
        var folder = new Folder(1L, "My Folder", "my-folder") { Id = 1L };

        _repository.SingleOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderBySlugSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        await _handler.Handle(new GetFolderBySlugQuery("my-folder"), TestContext.Current.CancellationToken);

        await _repository.Received(1).SingleOrDefaultAsync(
            Arg.Any<FolderSpecifications.FolderBySlugSpec>(),
            Arg.Any<CancellationToken>());
    }
}