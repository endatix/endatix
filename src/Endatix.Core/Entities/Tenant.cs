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
        /// <summary>
        /// Unique entity constraints that enforce tenant identity.
        /// Values should be used as domain and database indexes to enforce uniqueness.
        /// </summary>
        public static class UniqueConstraints
        {
            /// <summary>Unique public short URL across the deployment. Unfiltered: soft-deleted rows still hold theirs.</summary>
            public const string ShortUrl = "IX_Tenants_ShortUrl";
        }

        private readonly List<Form> _forms = [];
        private readonly List<FormDefinition> _formDefinitions = [];
        private readonly List<Submission> _submissions = [];

        private Tenant() { } // For EF Core

        /// <summary>
        /// Creates a tenant with a unique immutable short URL identifier.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="shortUrl">Server-generated 8-character lowercase alphanumeric identifier. Not derived from <paramref name="name"/>.</param>
        /// <param name="description">Optional description.</param>
        public Tenant(string name, string shortUrl, string? description = null)
        {
            Guard.Against.NullOrEmpty(name);
            Guard.Against.NullOrEmpty(shortUrl);
            Guard.Against.InvalidInput(
                shortUrl,
                nameof(shortUrl),
                value => Common.ShortUrl.IsValid(value),
                "ShortUrl must be an 8-character lowercase alphanumeric identifier.");

            Name = name;
            ShortUrl = shortUrl;
            Description = description;
        }

        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Unique short URL for unauthenticated discovery. Immutable after create.
        /// </summary>
        public string ShortUrl { get; private set; } = string.Empty;

        public string? Description { get; private set; }

        public IReadOnlyCollection<Form> Forms => _forms.AsReadOnly();
        public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();
        public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

        [NotMapped]
        public ICollection<User> Users { get; set; } = new List<User>();

        public TenantSettings? Settings { get; private set; }

        /// <summary>
        /// Updates the tenant display name. Does not change <see cref="ShortUrl"/>.
        /// </summary>
        public void UpdateName(string name)
        {
            Guard.Against.NullOrEmpty(name);
            Name = name;
        }

        /// <summary>
        /// Updates the tenant description.
        /// </summary>
        public void UpdateDescription(string? description)
        {
            Description = description;
        }

        public void RaiseCreated() => RegisterDomainEvent(new TenantCreatedEvent(this));

        public void RaiseUpdated(TenantSettings? settings) =>
            RegisterDomainEvent(new TenantUpdatedEvent(this, settings));

        public void RaiseContextChanged(
            long actorUserId,
            long fromTenantId,
            long toTenantId,
            TenantContextChangedEvent.Kind changeKind,
            DateTime occurredAt)
            => RegisterDomainEvent(new TenantContextChangedEvent(actorUserId, fromTenantId, toTenantId, changeKind, occurredAt));
    }
}
