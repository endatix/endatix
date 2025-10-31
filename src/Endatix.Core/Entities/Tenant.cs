using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities
{
    public class Tenant : BaseEntity, IAggregateRoot
    {
        private readonly List<Form> _forms = [];
        private readonly List<FormDefinition> _formDefinitions = [];
        private readonly List<Submission> _submissions = [];

        private Tenant() { } // For EF Core

        public Tenant(string name, string? description = null)
        {
            Guard.Against.NullOrEmpty(name, nameof(name));

            Name = name;
            Description = description;
        }

        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }

        public IReadOnlyCollection<Form> Forms => _forms.AsReadOnly();
        public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();
        public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

        [NotMapped]
        public ICollection<User> Users { get; set; } = new List<User>();

        public TenantSettings? Settings { get; private set; }
    }
}
