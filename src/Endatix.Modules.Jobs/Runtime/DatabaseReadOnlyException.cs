namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The startup probe found that every new transaction on the jobs database is read-only: a PostgreSQL standby, or a
/// database with default_transaction_read_only on.
/// </summary>
internal sealed class DatabaseReadOnlyException(string databaseName)
    : Exception($"Every new transaction on database '{databaseName}' is read-only: it is a standby, or default_transaction_read_only is on.");
