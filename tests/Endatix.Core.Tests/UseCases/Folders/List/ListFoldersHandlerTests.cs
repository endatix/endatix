using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.List;

namespace Endatix.Core.Tests.UseCases.Folders.List;

public sealed class ListFoldersHandlerTests
{
    private readonly IRepository<Folder> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ListFoldersHandler _handler;

    public ListFoldersHandlerTests()
    {
        _repository = Substitute.For<IRepository<Folder>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(1L);
        _handler = new ListFoldersHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_IncludeInactiveFalse_UsesActiveSpec()
    {
        var folders = new List<Folder>
        {
            new Folder(1L, "Alpha", "alpha", "ALPHA") { Id = 1L },
        };
        _repository.ListAsync(
                Arg.Any<FolderSpecifications.ActiveFoldersSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folders);

        var result = await _handler.Handle(new ListFoldersQuery(IncludeInactive: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value!.First().Slug.Should().Be("alpha");
        await _repository.Received(1).ListAsync(
            Arg.Any<FolderSpecifications.ActiveFoldersSpec>(),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ListAsync(
            Arg.Any<FolderSpecifications.AllFoldersSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IncludeInactiveTrue_UsesAllFoldersSpec()
    {
        var folders = new List<Folder>
        {
            new Folder(1L, "Beta", "beta", "BETA") { Id = 2L, IsActive = false },
        };
        _repository.ListAsync(
                Arg.Any<FolderSpecifications.AllFoldersSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folders);

        var result = await _handler.Handle(new ListFoldersQuery(IncludeInactive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.First().IsActive.Should().BeFalse();
        await _repository.Received(1).ListAsync(
            Arg.Any<FolderSpecifications.AllFoldersSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidTenantId_ThrowsArgumentException()
    {
        _tenantContext.TenantId.Returns(0);

        var request = new ListFoldersQuery();

        Func<Task> act = () => _handler.Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyResult()
    {
        _repository.ListAsync(
                Arg.Any<FolderSpecifications.ActiveFoldersSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        var result = await _handler.Handle(new ListFoldersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidFolder_MapsAllProperties()
    {
        var folder = new Folder(1L, "Test Folder", "test-folder", "TEST FOLDER")
        {
            Id = 42L,
            Description = "Test description",
            Metadata = @"{""key"": ""value""}",
            IsActive = true,
            Immutable = true
        };

        _repository.ListAsync(
                Arg.Any<FolderSpecifications.ActiveFoldersSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder> { folder });

        var result = await _handler.Handle(new ListFoldersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.First();
        dto.Id.Should().Be(42L);
        dto.Name.Should().Be("Test Folder");
        dto.Slug.Should().Be("test-folder");
        dto.Description.Should().Be("Test description");
        dto.Metadata.Should().Be(@"{""key"": ""value""}");
        dto.IsActive.Should().BeTrue();
        dto.Immutable.Should().BeTrue();
    }
}
