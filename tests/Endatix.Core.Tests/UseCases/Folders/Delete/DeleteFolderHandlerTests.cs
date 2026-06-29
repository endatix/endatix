using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.Delete;
using MediatR;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Folders.Delete;

public class DeleteFolderHandlerTests
{
    private readonly IRepository<Folder> _folderRepository;
    private readonly IRepository<Form> _formRepository;
    private readonly IRepository<FormTemplate> _formTemplateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly DeleteFolderHandler _handler;

    public DeleteFolderHandlerTests()
    {
        _folderRepository = Substitute.For<IRepository<Folder>>();
        _formRepository = Substitute.For<IRepository<Form>>();
        _formTemplateRepository = Substitute.For<IRepository<FormTemplate>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mediator = Substitute.For<IMediator>();
        _handler = new DeleteFolderHandler(
            _folderRepository,
            _formRepository,
            _formTemplateRepository,
            _tenantContext,
            _unitOfWork,
            _mediator);
    }

    [Fact]
    public async Task Handle_InvalidTenantId_ThrowsArgumentException()
    {
        _tenantContext.TenantId.Returns(0);

        var request = new DeleteFolderCommand(1L);

        Func<Task> act = () => _handler.Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_FolderNotFound_ReturnsNotFoundResult()
    {
        _tenantContext.TenantId.Returns(1L);
        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((Folder?)null);

        var request = new DeleteFolderCommand(1L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImmutableFolder_ReturnsConflictResult()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Locked", "locked", "LOCKED") { Id = 10L, Immutable = true };
        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().Contain(e => e.Contains("locked", StringComparison.OrdinalIgnoreCase));
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FolderWithForms_ClearsFolderAssignments()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Test", "test", "TEST") { Id = 10L };
        var forms = new List<Form>
        {
            new Form(1L, "Form 1", folderId: 10L) { Id = 1 },
            new Form(1L, "Form 2", folderId: 10L) { Id = 2 }
        };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(forms);

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<FormTemplate>());

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        forms[0].FolderId.Should().BeNull();
        forms[1].FolderId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FolderWithTemplates_ClearsFolderAssignments()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Test", "test", "TEST") { Id = 10L };
        var templates = new List<FormTemplate>
        {
            new FormTemplate(1L, "Template 1", folderId: 10L) { Id = 1 },
            new FormTemplate(1L, "Template 2", folderId: 10L) { Id = 2 }
        };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(templates);

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        templates[0].FolderId.Should().BeNull();
        templates[1].FolderId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FolderWithChildFolders_ReparentsToRoot()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Parent", "parent", "PARENT") { Id = 10L };
        var childFolders = new List<Folder>
        {
            new Folder(1L, "Child 1", "child-1", "CHILD-1") { Id = 11L, ParentFolderId = 10L },
            new Folder(1L, "Child 2", "child-2", "CHILD-2") { Id = 12L, ParentFolderId = 10L }
        };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<FormTemplate>());

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(childFolders);

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        childFolders[0].ParentFolderId.Should().BeNull();
        childFolders[1].ParentFolderId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesFolderAndCommits()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Test", "test", "TEST") { Id = 10L };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<FormTemplate>());

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("10");
        await _folderRepository.Received(1).DeleteAsync(folder, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesFolderDeletedEvent()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Test", "test", "TEST") { Id = 10L };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<FormTemplate>());

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        var request = new DeleteFolderCommand(10L);
        await _handler.Handle(request, CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<FolderDeletedEvent>(e => e.Folder.Id == folder.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryException_ReturnsErrorResult()
    {
        _tenantContext.TenantId.Returns(1L);
        var folder = new Folder(1L, "Test", "test", "TEST") { Id = 10L };

        _folderRepository.FirstOrDefaultAsync(
                Arg.Any<FolderSpecifications.FolderByIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(folder);

        _formRepository.ListAsync(
                Arg.Any<FormSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _formTemplateRepository.ListAsync(
                Arg.Any<FormTemplateSpecifications.ByFolderId>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<FormTemplate>());

        _folderRepository.ListAsync(
                Arg.Any<FolderSpecifications.ByParentFolderIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Folder>());

        _folderRepository.DeleteAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Database error"));

        var request = new DeleteFolderCommand(10L);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Error deleting folder"));
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }
}