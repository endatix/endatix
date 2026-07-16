using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportMappings;

namespace Endatix.Modules.Reporting.Tests.Features.ExportMappings;

public sealed class ListExportMappingsHandlerTests
{
    private const long TenantId = 1;

    [Fact]
    public async Task Handle_WhenTenantIdInvalid_ReturnsUnauthorized()
    {
        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        ListExportMappingsHandler handler = new(repository);

        Result<List<ExportMappingDto>> result = await handler.Handle(
            new ListExportMappingsQuery(TenantId: 0),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Unauthorized);
        await repository.DidNotReceive().ListAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsMappingsFromRepository()
    {
        List<ExportMappingDto> mappings =
        [
            new(Id: 1, ExportFormatId: 1, SurveyTypeId: null, IsDefault: true, ExportFormat: null),
            new(Id: 2, ExportFormatId: 2, SurveyTypeId: 100, IsDefault: false, ExportFormat: null),
        ];

        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        repository.ListAsync(TenantId, Arg.Any<CancellationToken>()).Returns(mappings);

        ListExportMappingsHandler handler = new(repository);

        Result<List<ExportMappingDto>> result = await handler.Handle(
            new ListExportMappingsQuery(TenantId),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(mappings);
    }

    [Fact]
    public async Task Handle_WhenRepositoryReturnsEmpty_ReturnsEmptyList()
    {
        IExportMappingRepository repository = Substitute.For<IExportMappingRepository>();
        repository.ListAsync(TenantId, Arg.Any<CancellationToken>()).Returns([]);

        ListExportMappingsHandler handler = new(repository);

        Result<List<ExportMappingDto>> result = await handler.Handle(
            new ListExportMappingsQuery(TenantId),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
