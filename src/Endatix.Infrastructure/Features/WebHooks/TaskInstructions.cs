namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents a set of task instructions for specific webhook operation.
/// </summary>
public class TaskInstructions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskInstructions"/> class with the specified URI.
    /// </summary>
    /// <param name="uri">The URI associated with the task instruction set.</param>
    public TaskInstructions(string uri)
    {
        Uri = uri;
    }

    /// <summary>
    /// Gets the URI associated with the task instruction set.
    /// </summary>
    public string Uri { get; init; }
}
