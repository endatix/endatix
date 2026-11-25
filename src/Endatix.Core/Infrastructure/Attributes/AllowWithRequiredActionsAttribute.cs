using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Infrastructure.Attributes;

/// <summary>Identifies an account operation available to resolve or leave a pending action.
/// Does not grant access to the operation.</summary>
[Experimental("ENDATIX0006")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class AllowWithRequiredActionsAttribute : Attribute;
