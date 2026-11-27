using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions;

/// <summary>A historical record whose stored values cannot be updated.</summary>
[Experimental("ENDATIX0007")]
public interface IInsertOnly;
