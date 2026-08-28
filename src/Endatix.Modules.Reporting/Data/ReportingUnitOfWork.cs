using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Data;

internal sealed class ReportingUnitOfWork(
    ReportingDbContext context,
    ILogger<ReportingUnitOfWork>? logger = null)
    : EfUnitOfWorkBase<ReportingDbContext>(context, logger), IReportingUnitOfWork;
