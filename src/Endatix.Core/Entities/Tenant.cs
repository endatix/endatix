using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Core.Common;
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

        /// <summary>
        /// Creates a tenant with a unique immutable public id stored as <see cref="Slug"/>.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="slug">Server-generated 8-character alphanumeric public id. Not derived from <paramref name="name"/>.</param>
        /// <param name="description">Optional description.</param>
        public Tenant(string name, string slug, string? description = null)
        {
            Guard.Against.NullOrEmpty(name, nameof(name));
            Guard.Against.NullOrEmpty(slug, nameof(slug));
            Guard.Against.InvalidInput(slug, nameof(slug), PublicId.IsValidTenantSlug, "Slug must be an 8-character public id.");

            Name = name;
            Slug = slug;
            Description = description;
        }

        public string Name { get; private set; } = null!;

        /// <summary>
        /// Unique public identifier for unauthenticated discovery (sign-in / self-reg). Immutable after create.
        /// </summary>
        public string Slug { get; private set; } = null!;

        public string? Description { get; private set; }

        public IReadOnlyCollection<Form> Forms => _forms.AsReadOnly();
        public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();
        public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

        [NotMapped]
        public ICollection<User> Users { get; set; } = new List<User>();

        public TenantSettings? Settings { get; private set; }

        /// <summary>
        /// Updates the tenant display name. Does not change <see cref="Slug"/>.
        /// </summary>
        public void UpdateName(string name)
        {
            Guard.Against.NullOrEmpty(name, nameof(name));
            Name = name;
        }

        /// <summary>
        /// Updates the tenant description.
        /// </summary>
        public void UpdateDescription(string? description)
        {
            Description = description;
        }
    }
}
