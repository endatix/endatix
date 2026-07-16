using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportMappings;

namespace Endatix.Modules.Reporting.Tests.Features.ExportMappings;

public sealed class UpsertExportMappingHandlerTests
{
    private const long TenantId = 1;

    [Fact]
    public async Task Handle_WhenTenantIdInvalid_ReturnsUnauthorized()
    {
        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        UpsertExportMappingHandler handler = new(repository);

        Result<ExportMappingDto> result = await handler.Handle(
            new UpsertExportMappingCommand(TenantId: 0, new UpsertExportMappingRequest(ExportFormatId: 1, SurveyTypeId: null, IsDefault: false)),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Unauthorized);
        await repository.DidNotReceive().UpsertAsync(Arg.Any<long>(), Arg.Any<UpsertExportMappingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExportFormatIdInvalid_ReturnsInvalid()
    {
        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        UpsertExportMappingHandler handler = new(repository);

        Result<ExportMappingDto> result = await handler.Handle(
            new UpsertExportMappingCommand(TenantId, new UpsertExportMappingRequest(ExportFormatId: 0, SurveyTypeId: null, IsDefault: false)),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        await repository.DidNotReceive().UpsertAsync(Arg.Any<long>(), Arg.Any<UpsertExportMappingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExportFormatNotFound_ReturnsNotFound()
    {
        UpsertExportMappingRequest request = new(ExportFormatId: 999, SurveyTypeId: null, IsDefault: true);

        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        repository.UpsertAsync(TenantId, request, Arg.Any<CancellationToken>()).Returns((ExportMappingDto?)null);

        UpsertExportMappingHandler handler = new(repository);

        Result<ExportMappingDto> result = await handler.Handle(
            new UpsertExportMappingCommand(TenantId, request),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsMapping()
    {
        UpsertExportMappingRequest request = new(ExportFormatId: 1, SurveyTypeId: null, IsDefault: true);
        ExportMappingDto mapping = new(Id: 1, ExportFormatId: 1, SurveyTypeId: null, IsDefault: true, ExportFormat: null);

        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        repository.UpsertAsync(TenantId, request, Arg.Any<CancellationToken>()).Returns(mapping);

        UpsertExportMappingHandler handler = new(repository);

        Result<ExportMappingDto> result = await handler.Handle(
            new UpsertExportMappingCommand(TenantId, request),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(mapping);
    }
}
