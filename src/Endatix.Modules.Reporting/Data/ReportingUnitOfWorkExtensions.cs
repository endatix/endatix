namespace Endatix.Modules.Reporting.Data;

internal static class ReportingUnitOfWorkExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> in one transaction and returns what it returned: committed when it completes,
    /// rolled back when it or the commit throws.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(
        this IReportingUnitOfWork unitOfWork,
        Func<Task<T>> work,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work();
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction: committed when it completes, rolled back when it or the commit
    /// throws.
    /// </summary>
    public static Task InTransactionAsync(
        this IReportingUnitOfWork unitOfWork,
        Func<Task> work,
        CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async () =>
            {
                await work();
                return true;
            },
            cancellationToken);
}
