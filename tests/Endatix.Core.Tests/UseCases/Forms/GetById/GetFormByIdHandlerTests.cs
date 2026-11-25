using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.GetById;

namespace Endatix.Core.Tests.UseCases.Forms.GetById;

public class GetFormByIdHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly GetFormByIdHandler _handler;

    public GetFormByIdHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _handler = new GetFormByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_NoTenantContext_DoesNotReadStandingOrAForm()
    {
        var auth = Substitute.For<ICurrentUserAuthorizationService>();
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(0L);
        var handler = new GetFormByIdHandler(_repository, new AccountStanding(auth), tenant);

        var result = await handler.Handle(new GetFormByIdQuery(1), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        auth.ReceivedCalls().Should().BeEmpty();
        _repository.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetFormByIdQuery(1);
        _repository.FirstOrDefaultAsync(Arg.Any<FormByIdWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
                   .Returns((FormDto?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsForm()
    {
        // Arrange
        var form = new FormDto { Id = "1", Name = SampleData.FORM_NAME_1, SubmissionsCount = 0 };
        var request = new GetFormByIdQuery(1);
        _repository.FirstOrDefaultAsync(Arg.Any<FormByIdWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(form);
    }
}
