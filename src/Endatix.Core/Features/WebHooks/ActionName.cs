using System.ComponentModel.DataAnnotations;

namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Enum for defining the types of actions that can be performed on an entity.
/// </summary>
public enum ActionName
{
    /// <summary>
    /// Represents the action of creating an entity.
    /// </summary>
    [Display(Name = "created")]
    Created,
    /// <summary>
    /// Represents the action of updating an entity.
    /// </summary>
    [Display(Name = "updated")]
    Updated,
    /// <summary>
    /// Represents the action of deleting an entity.
    /// </summary>
    [Display(Name = "deleted")]
    Deleted,
}
