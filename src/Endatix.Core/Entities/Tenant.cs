using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Core.Common;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
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
        /// Creates a tenant with a unique immutable slug.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="slug">Normalized unique slug (set only at create).</param>
        /// <param name="description">Optional description.</param>
        public Tenant(string name, string slug, string? description = null)
        {
            Guard.Against.NullOrEmpty(name, nameof(name));
            Guard.Against.NullOrEmpty(slug, nameof(slug));
            Guard.Against.InvalidInput(slug, nameof(slug), UrlSlugNormalizer.IsValidFormat, "Slug format is invalid.");
            Guard.Against.InvalidInput(slug, nameof(slug), s => !UrlSlugNormalizer.IsReserved(s), "Slug is reserved.");

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
        /// Updates the tenant display name.
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

        /// <summary>
        /// Raises <see cref="TenantCreatedEvent"/> so the outbox captures it in the same transaction that
        /// persists the tenant. Called by the create use case once the aggregate is complete.
        /// </summary>
        public void RaiseCreated() => RegisterDomainEvent(new TenantCreatedEvent(this));
    }
}
