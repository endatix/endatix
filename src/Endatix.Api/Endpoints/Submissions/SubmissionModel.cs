﻿namespace Endatix.Api.Endpoints.Submissions;

public class SubmissionModel
{
    public string Id { get; set; }
    public bool IsComplete { get; set; }
    public string JsonData { get; set; }
    public string FormId { get; set; }
    public string FormDefinitionId { get; set; }
    public int? CurrentPage { get; set; }
    public string? Metadata { get; set; }
    public string? Token { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string Status { get; set; }
}