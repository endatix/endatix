using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Data;

internal sealed class ReportingUnitOfWork(
    ReportingDbContextBase context,
    ILogger<ReportingUnitOfWork>? logger = null)
    : EfUnitOfWorkBase<ReportingDbContextBase>(context, logger), IReportingUnitOfWork;
