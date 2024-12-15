using Endatix.Api.Endpoints.FormDefinitions;

namespace Endatix.Api.Endpoints.Submissions;

public class SubmissionDetailsModel : SubmissionModel
{
    public FormDefinitionModel? FormDefinition { get; set; }
}
