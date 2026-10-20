using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.DeleteTests;

namespace Endatix.Core.Tests.UseCases.Submissions.DeleteTests;

public class DeleteTestSubmissionsHandlerTests
{
    private const long TenantId = SampleData.TENANT_ID;
    private const long FormId = 10;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeleteTestSubmissionsHandler _handler;

    public DeleteTestSubmissionsHandlerTests()
    {
        _handler = new DeleteTestSubmissionsHandler(_submissions, _forms, _unitOfWork);
        var form = Form.Create(FormId, new FormCreateArgs(TenantId: TenantId, Name: "Form"));
        _forms.FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<FormSpecifications.ByIdForTenant>().IsSatisfiedBy(form) ? form : null);
    }

    private static Submission TestSubmission(long id) =>
        Submission.Create(id, new SubmissionCreateArgs(
            TenantId: TenantId, FormId: FormId, FormDefinitionId: 1, JsonData: "{}", IsTestSubmission: true));

    [Fact]
    public async Task Handle_NoTenant_IsUnauthorizedAndDeletesNothing()
    {
        // Act
        var result = await _handler.Handle(new DeleteTestSubmissionsCommand(0, FormId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _submissions.DidNotReceive().ListAsync(Arg.Any<TestSubmissionsByFormIdPageSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FormOfAnotherTenant_ReturnsNotFoundAndDeletesNothing()
    {
        // Act
        var result = await _handler.Handle(new DeleteTestSubmissionsCommand(TenantId + 1, FormId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _submissions.DidNotReceive().ListAsync(Arg.Any<TestSubmissionsByFormIdPageSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FormMissing_ReturnsNotFound()
    {
        // Arrange
        _forms.FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        // Act
        var result = await _handler.Handle(new DeleteTestSubmissionsCommand(TenantId, FormId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_PagesOfTestSubmissions_DeletesEachAndSavesPerPage()
    {
        // Arrange
        var firstPage = Enumerable.Range(1, DeleteTestSubmissionsHandler.PAGE_SIZE).Select(i => TestSubmission(i)).ToList();
        var secondPage = new List<Submission> { TestSubmission(DeleteTestSubmissionsHandler.PAGE_SIZE + 1) };
        _submissions.ListAsync(Arg.Any<TestSubmissionsByFormIdPageSpec>(), Arg.Any<CancellationToken>())
            .Returns(firstPage, secondPage);

        // Act
        var result = await _handler.Handle(new DeleteTestSubmissionsCommand(TenantId, FormId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(DeleteTestSubmissionsHandler.PAGE_SIZE + 1);
        firstPage.Concat(secondPage).Should().OnlyContain(s => s.IsDeleted);
        firstPage[0].DomainEvents.OfType<SubmissionDeletedEvent>().Should().ContainSingle();
        await _submissions.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PagesOfTestSubmissions_ClearsTheChangeTrackerAfterEachSave()
    {
        // Arrange
        var firstPage = Enumerable.Range(1, DeleteTestSubmissionsHandler.PAGE_SIZE).Select(i => TestSubmission(i)).ToList();
        var secondPage = new List<Submission> { TestSubmission(DeleteTestSubmissionsHandler.PAGE_SIZE + 1) };
        _submissions.ListAsync(Arg.Any<TestSubmissionsByFormIdPageSpec>(), Arg.Any<CancellationToken>())
            .Returns(firstPage, secondPage);

        // Act
        await _handler.Handle(new DeleteTestSubmissionsCommand(TenantId, FormId), CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            _submissions.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.ClearChangeTracker();
            _submissions.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.ClearChangeTracker();
        });
    }

    [Fact]
    public async Task Handle_NoTestSubmissions_ReturnsZero()
    {
        // Arrange
        _submissions.ListAsync(Arg.Any<TestSubmissionsByFormIdPageSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<Submission>());

        // Act
        var result = await _handler.Handle(new DeleteTestSubmissionsCommand(TenantId, FormId), CancellationToken.None);

        // Assert
        result.Value.Count.Should().Be(0);
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
