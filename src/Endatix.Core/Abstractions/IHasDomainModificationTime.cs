namespace Endatix.Core.Abstractions;

/// <summary>The domain timestamps every change, including two saves at the same clock instant.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public interface IHasDomainModificationTime
{
    /// <summary>UTC timestamp supplied by the writer.</summary>
    DateTime? ModifiedAt { get; }
}
