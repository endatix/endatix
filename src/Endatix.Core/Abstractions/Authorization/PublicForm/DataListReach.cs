namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>Which of a form's definitions a public data-list read may follow to a list.</summary>
public enum DataListReach
{
    /// <summary>Only the active definition: what the runtime serves now. Used by search.</summary>
    ActiveDefinition,

    /// <summary>Any definition of the form, drafts and older ones included. Used by display values.</summary>
    AnyDefinition,
}
