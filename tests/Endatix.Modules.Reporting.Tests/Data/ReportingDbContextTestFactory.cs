using Endatix.Core.Abstractions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Tests.Data;

internal static class ReportingDbContextTestFactory
{
    internal static ReportingDbContext Create(long tenantId = 1)
    {
        DbContextOptions<ReportingDbContext> options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        return new ReportingDbContext(options, new SequentialIdGenerator(), tenantContext);
    }

    internal static FormExportSchemaRepository CreateRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormExportSchemaRepository(dbContext, unitOfWork);
    }
}

internal sealed class SequentialIdGenerator : IIdGenerator<long>
{
    private long _next = 1;

    public long CreateId() => _next++;
}
