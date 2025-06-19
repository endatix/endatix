using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetFiles;
using Endatix.Core.Specifications;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Tests.UseCases.Submissions.GetFiles;

public class GetFilesHandlerTests
{
    private readonly IRepository<Submission> _submissionRepo = Substitute.For<IRepository<Submission>>();
    private readonly IFormsRepository _formRepo = Substitute.For<IFormsRepository>();
    private readonly ISubmissionFileExtractor _extractor = Substitute.For<ISubmissionFileExtractor>();
    private readonly GetFilesHandler _handler;

    public GetFilesHandlerTests()
    {
        _handler = new GetFilesHandler(_submissionRepo, _formRepo, _extractor);
    }

    [Fact]
    public async Task Handle_SubmissionOrFormNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 2L;
        var query = new GetFilesQuery(formId, submissionId, null);

        _submissionRepo
            .SingleOrDefaultAsync(Arg.Is<SubmissionWithDefinitionSpec>(spec => true), Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        _formRepo
            .GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Form?>(null));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_FoundWithFiles_ReturnsFiles()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 2L;
        var prefix = "prefix";
        var query = new GetFilesQuery(formId, submissionId, prefix);

        var submission = new Submission(1, "{}", formId, 1) { Id = submissionId };
        var form = new Form(1, "TestForm");

        _submissionRepo
            .SingleOrDefaultAsync(Arg.Is<SubmissionWithDefinitionSpec>(spec => true), Arg.Any<CancellationToken>())
            .Returns(submission);

        _formRepo
            .GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Form?)form));

        var files = new List<ISubmissionFileExtractor.ExtractedFile>
        {
            new("file.txt", "text/plain", new System.IO.MemoryStream([1, 2, 3]))
        };

        _extractor
            .ExtractFilesAsync(Arg.Any<System.Text.Json.JsonElement>(), submissionId, prefix, Arg.Any<CancellationToken>())
            .Returns(files);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().HaveCount(1);
        result.Value.Files[0].FileName.Should().Be("file.txt");
    }

    [Fact]
    public async Task Handle_FoundWithNoFiles_ReturnsEmptyFiles()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 2L;
        var query = new GetFilesQuery(formId, submissionId, null);

        var submission = new Submission(1, "{}", formId, 1) { Id = submissionId };
        var form = new Form(1, "TestForm");

        _submissionRepo
            .SingleOrDefaultAsync(Arg.Is<SubmissionWithDefinitionSpec>(spec => true), Arg.Any<CancellationToken>())
            .Returns(submission);

        _formRepo
            .GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Form?)form));

        _extractor
            .ExtractFilesAsync(Arg.Any<System.Text.Json.JsonElement>(), submissionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<ISubmissionFileExtractor.ExtractedFile>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().BeEmpty();
    }
}