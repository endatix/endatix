namespace Endatix.Modules.Jobs.Runtime;

internal static class ExceptionCauses
{
    /// <summary>
    /// Whether <paramref name="failure"/>, or any exception it wraps, is <paramref name="cause"/>: the scheduler and
    /// EF Core wrap the driver's exception in their own.
    /// </summary>
    public static bool HasCause(this Exception failure, Func<Exception, bool> cause)
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (cause(current))
            {
                return true;
            }
        }

        return false;
    }
}
