using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Data;

internal sealed class ReportingUnitOfWork(ReportingDbContext context)
    : EfUnitOfWorkBase<ReportingDbContext>(context), IReportingUnitOfWork;
