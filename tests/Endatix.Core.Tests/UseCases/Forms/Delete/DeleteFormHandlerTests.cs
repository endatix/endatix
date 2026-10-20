using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.Forms.Delete;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Forms.Delete;

public class DeleteFormHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly IFormSubmissionsLock _formSubmissionsLock;
    private readonly IFormSubmissionsSection _section;
    private readonly IMediator _mediator;
    private readonly DeleteFormHandler _handler;

    public DeleteFormHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        var formSubmissionsLock = new FormSubmissionsLockStub();
        _formSubmissionsLock = formSubmissionsLock.Lock;
        _section = formSubmissionsLock.Section;
        _repository.AnyAsync(Arg.Any<FormSpecifications.ByIdReadOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        _mediator = Substitute.For<IMediator>();
        _handler = new DeleteFormHandler(_repository, _formSubmissionsLock, _mediator);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundWithoutTakingTheLock()
    {
        // Arrange
        var request = new DeleteFormCommand(1);
        _repository.AnyAsync(Arg.Any<FormSpecifications.ByIdReadOnly>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _formSubmissionsLock.DidNotReceive().BeginDeletingAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeletingSubmissionsFails_NeitherCommitsNorPublishes()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(form.Id);
        _repository.SingleOrDefaultAsync(Arg.Any<FormWithDefinitionsSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);
        _repository.DeleteSubmissionsAsync(form, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("database unavailable"));

        // Act
        var act = () => _handler.Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _section.Received(1).DisposeAsync();
        await _mediator.DidNotReceive().Publish(Arg.Any<FormDeletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FormDeletedWhileWaitingForTheLock_ReturnsNotFoundWithoutCommitting()
    {
        // Arrange
        var request = new DeleteFormCommand(1);
        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _section.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Handle_FormLockUnavailable_ReturnsUnavailableWithoutReadingTheForm()
    {
        // Arrange
        var request = new DeleteFormCommand(1);
        _formSubmissionsLock.BeginDeletingAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Unavailable("busy"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _repository.DidNotReceive().SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsSpec>(),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Publish(Arg.Any<FormDeletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeletionChosenAsDeadlockVictim_ReturnsUnavailableWithoutPublishing()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(form.Id);
        _repository.SingleOrDefaultAsync(Arg.Any<FormWithDefinitionsSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);
        _repository.DeleteSubmissionsAsync(form, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new DeadlockVictimException("deadlock"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Publish(Arg.Any<FormDeletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesForm()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(1);

        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(form);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Form>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesSubmissionsBeforeTheFormThenCommitsUnderTheLock()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(form.Id);
        _repository.SingleOrDefaultAsync(Arg.Any<FormWithDefinitionsSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            _formSubmissionsLock.BeginDeletingAsync(form.Id, Arg.Any<CancellationToken>());
            _repository.SingleOrDefaultAsync(Arg.Any<FormWithDefinitionsSpec>(), Arg.Any<CancellationToken>());
            _repository.DeleteSubmissionsAsync(form, Arg.Any<CancellationToken>());
            _repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _section.CommitAsync(Arg.Any<CancellationToken>());
            _mediator.Publish(Arg.Any<FormDeletedEvent>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsDeleteOnForm()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(1);

        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        form.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesFormDeletedEvent()
    {
        // Arrange
        var form = CreateForm();
        var request = new DeleteFormCommand(1);

        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Publish(
            Arg.Is<FormDeletedEvent>(e => e.Form == form),
            Arg.Any<CancellationToken>()
        );
    }

    private static Form CreateForm()
    {
        var form = Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form"));
        form.Id = 1;
        return form;
    }
}
