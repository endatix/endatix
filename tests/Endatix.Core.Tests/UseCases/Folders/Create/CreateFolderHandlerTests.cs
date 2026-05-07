using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.Create;
using Endatix.Core.UseCases.Folders;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Folders.Create;

public class CreateFolderHandlerTests
{
    private readonly ITenantContext _tenantContext;
    private readonly IRepository<Folder> _repository;
    private readonly IMediator _mediator;
    private readonly IValueNormalizer _valueNormalizer;
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker;
    private readonly FolderWritePolicy _folderWritePolicy;
    private readonly CreateFolderHandler _sut;

    public CreateFolderHandlerTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
        _repository = Substitute.For<IRepository<Folder>>();
        _mediator = Substitute.For<IMediator>();
        _valueNormalizer = Substitute.For<IValueNormalizer>();
        _uniqueConstraintViolationChecker = Substitute.For<IUniqueConstraintViolationChecker>();
        _folderWritePolicy = new FolderWritePolicy(_repository, _valueNormalizer);
        _sut = new CreateFolderHandler(_repository, _mediator, _tenantContext, _folderWritePolicy, _uniqueConstraintViolationChecker);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCreated()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder", "test-folder", null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidCommand_SetsCorrectName()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder ", "test-folder", null),
            TestContext.Current.CancellationToken);

        result.Value!.Name.Should().Be("Test Folder");
    }

    [Fact]
    public async Task Handle_ValidCommand_NormalizesSlug()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder", "TEST-FOLDER", null),
            TestContext.Current.CancellationToken);

        result.Value!.UrlSlug.Should().Be("test-folder");
    }

    [Fact]
    public async Task Handle_NullSlug_AutoGeneratesFromName()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("my-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "My Folder", "my-folder", "my-folder"));

        var result = await _sut.Handle(
            new CreateFolderCommand("My Folder", null, null),
            TestContext.Current.CancellationToken);

        result.Value!.UrlSlug.Should().Be("my-folder");
    }

    [Fact]
    public async Task Handle_ImmutableTrue_SetsImmutable()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder", "test-folder", null, null, true),
            TestContext.Current.CancellationToken);

        result.Value!.Immutable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDescription_SetsDescription()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder", "A description"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder", "test-folder", "A description"),
            TestContext.Current.CancellationToken);

        result.Value!.Description.Should().Be("A description");
    }

    [Fact]
    public async Task Handle_WithMetadata_SetsMetadata()
    {
        _tenantContext.TenantId.Returns(101);
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns("test-folder");
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsByNormalizedNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AnyAsync(Arg.Any<FolderSpecifications.FolderExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.AddAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(info => new Folder(101, "Test Folder", "test-folder", "test-folder", null, null, @"{""key"": ""value""}"));

        var result = await _sut.Handle(
            new CreateFolderCommand("Test Folder", "test-folder", null, @"{""key"": ""value""}"),
            TestContext.Current.CancellationToken);

        result.Value!.Metadata.Should().Be(@"{""key"": ""value""}");
    }

    [Fact]
    public void Handle_WithNullName_ThrowsArgumentException()
    {
        Action act = () => _ = new CreateFolderCommand(null!, "slug", null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handle_WithWhitespaceName_ThrowsArgumentException()
    {
        Action act = () => _ = new CreateFolderCommand("   ", "slug", null);

        act.Should().Throw<ArgumentException>();
    }
}