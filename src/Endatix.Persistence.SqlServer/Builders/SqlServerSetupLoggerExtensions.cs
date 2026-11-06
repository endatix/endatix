using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Persistence.SqlServer.Builders;

/// <summary>
/// Source-generated logging for SQL Server persistence setup.
/// </summary>
internal static partial class SqlServerSetupLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.PersistenceSetupMessage,
        Level = LogLevel.Debug,
        Message = " ❯ {Category}: {Message} ✔️")]
    public static partial void LogSetupInfo(this ILogger logger, string category, string message);
}
