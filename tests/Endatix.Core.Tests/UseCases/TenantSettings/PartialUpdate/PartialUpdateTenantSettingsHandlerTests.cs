using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.TenantSettings;
using Endatix.Core.UseCases.TenantSettings.Get;
using Endatix.Core.UseCases.TenantSettings.PartialUpdate;
using MediatR;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.TenantSettings.PartialUpdate;

public class PartialUpdateTenantSettingsHandlerTests
{
    private readonly IRepository<TenantSettingsEntity> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly PartialUpdateTenantSettingsHandler _handler;

    public PartialUpdateTenantSettingsHandlerTests()
    {
        _repository = Substitute.For<IRepository<TenantSettingsEntity>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _mediator = Substitute.For<IMediator>();
        _handler = new PartialUpdateTenantSettingsHandler(_repository, _tenantContext, _mediator);
    }

    [Fact]
    public async Task Handle_TenantSettingsNotFound_ReturnsNotFoundResult()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(
                Arg.Any<TenantSettingsByTenantIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((TenantSettingsEntity?)null);

        var command = new PartialUpdateTenantSettingsCommand();
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Tenant settings not found.");
    }

    [Fact]
    public async Task Handle_NoPropertiesSet_ReturnsCurrentSettings()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(
                Arg.Any<TenantSettingsByTenantIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(settings);

        var getResult = Result.Success(new TenantSettingsDto
        {
            TenantId = SampleData.TENANT_ID,
            RequireFolderAssignment = false
        });
        _mediator.Send(Arg.Any<GetTenantSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(getResult);

        var command = new PartialUpdateTenantSettingsCommand();
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<TenantSettingsEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequireFolderAssignmentTrue_UpdatesAndReturnsSettings()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(
                Arg.Any<TenantSettingsByTenantIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(settings);

        var getResult = Result.Success(new TenantSettingsDto
        {
            TenantId = SampleData.TENANT_ID,
            RequireFolderAssignment = true
        });
        _mediator.Send(Arg.Any<GetTenantSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(getResult);

        var command = new PartialUpdateTenantSettingsCommand { RequireFolderAssignment = true };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequireFolderAssignment.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequireFolderAssignmentFalse_UpdatesAndReturnsSettings()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        settings.UpdateRequireFolderAssignment(true);
        _repository.FirstOrDefaultAsync(
                Arg.Any<TenantSettingsByTenantIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(settings);

        var getResult = Result.Success(new TenantSettingsDto
        {
            TenantId = SampleData.TENANT_ID,
            RequireFolderAssignment = false
        });
        _mediator.Send(Arg.Any<GetTenantSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(getResult);

        var command = new PartialUpdateTenantSettingsCommand { RequireFolderAssignment = false };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequireFolderAssignment.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsGetQueryToReturnResult()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(
                Arg.Any<TenantSettingsByTenantIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(settings);

        var getResult = Result.Success(new TenantSettingsDto
        {
            TenantId = SampleData.TENANT_ID,
            SubmissionTokenExpiryHours = 24,
            RequireFolderAssignment = true
        });
        _mediator.Send(Arg.Any<GetTenantSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(getResult);

        var command = new PartialUpdateTenantSettingsCommand { RequireFolderAssignment = true };
        var result = await _handler.Handle(command, CancellationToken.None);

        await _mediator.Received(1).Send(new GetTenantSettingsQuery(), Arg.Any<CancellationToken>());
    }
}