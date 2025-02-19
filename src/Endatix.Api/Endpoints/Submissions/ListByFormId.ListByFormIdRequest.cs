﻿using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request object to get list of submissions for a given form
/// </summary>
public class ListByFormIdRequest : IPagedRequest, IFilteredRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The number of the page
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// The filter expressions
    /// </summary>
    public IEnumerable<string>? Filter { get; set; }
}
