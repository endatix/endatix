﻿using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification for retrieving a form submission. Note that both Ids are required - the submission Id and the respective form Id it belongs to
/// </summary>
public class SubmissionWithDefinitionSpec : SingleResultSpecification<Submission>
{
    public SubmissionWithDefinitionSpec(long formId, long submissionId)
    {
        Query
            .Include(s => s.FormDefinition) 
            .Where(s => s.Id == submissionId && s.FormDefinition.FormId == formId)
            .AsNoTracking();
    }
}
