namespace Endatix.Core.Abstractions.Authorization;

/// <summary>Revalidates the current operator and target independently of required-action standing.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0006")]
public abstract class VerifiedAssumptionReader
{
    private protected VerifiedAssumptionReader() { }

    internal abstract Task<VerifiedAssumption?> ReadAsync(CancellationToken cancellationToken);
}
