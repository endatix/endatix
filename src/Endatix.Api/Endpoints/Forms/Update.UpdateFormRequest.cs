﻿namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for updating a form.
/// </summary>
public class UpdateFormRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The name of the form.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The description of the form.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if the form is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }
}
