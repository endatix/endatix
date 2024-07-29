using System.Collections.Generic;
using Endatix.Core.Configuration;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class FormDefinition : BaseEntity, IAggregateRoot
{

    public bool IsDraft { get; set; }
    public int Version { get; set; }
    public string JsonData { get; internal set; }
    public long FormId { get; set; }
    public Form Form { get; set; }
    public bool IsActive { get; set; }
    private readonly List<Submission> _submissions = [];
    public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

    public FormDefinition(bool isDraft = false, string jsonData = null, bool isActive = true)
    {
        jsonData ??= EndatixConfig.Configuration.DefaultFormDefinitionJson;
        IsDraft = isDraft;
        JsonData = jsonData;
        IsActive = isActive;
    }
}