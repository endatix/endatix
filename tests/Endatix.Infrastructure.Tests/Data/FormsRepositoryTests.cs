using Endatix.Core.Entities;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Infrastructure.Repositories;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Data;

public class FormsRepositoryTests
{
    private readonly AppDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FormsRepository _sut;

    public FormsRepositoryTests()
    {
        _dbContext = Substitute.For<AppDbContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new FormsRepository(_dbContext, _unitOfWork);
    }

    [Fact]
    public async Task CreateFormWithDefinitionAsync_ShouldCommitTransaction_WhenSuccessful()
    {
        // Arrange
        var form = new Form("form", "description", true);
        var formDefinition = new FormDefinition();

        // Act
        await _sut.CreateFormWithDefinitionAsync(form, formDefinition);

        // Assert
        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFormWithDefinitionAsync_ShouldRollbackTransaction_WhenExceptionThrown()
    {
        // Arrange
        var form = new Form("form", "description", true);
        var formDefinition = new FormDefinition();
        _dbContext.Set<FormDefinition>().When(x => x.Add(formDefinition)).Do(x => { throw new Exception("Test exception"); });

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _sut.CreateFormWithDefinitionAsync(form, formDefinition));

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }
}