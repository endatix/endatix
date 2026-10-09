namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal sealed class SchemaCompilationLimitExceededException : InvalidOperationException
{
    public SchemaCompilationLimitExceededException(
        SchemaCompilationLimitKind limitKind,
        int limit,
        string message,
        int? actual = null,
        string? context = null) : base(message)
    {
        LimitKind = limitKind;
        Limit = limit;
        Actual = actual;
        Context = context;
    }

    public SchemaCompilationLimitKind LimitKind { get; }

    public int Limit { get; }

    public int? Actual { get; }

    public string? Context { get; }

    /// <summary>The failure as the form's compile reports it, naming the form and the limit it ran into.</summary>
    public InvalidOperationException ForForm(long formId) =>
        new($"Form schema compilation failed for form {formId}: {LimitKind}.", this);
}
