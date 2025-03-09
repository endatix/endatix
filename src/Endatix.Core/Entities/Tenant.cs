using System.ComponentModel.DataAnnotations.Schema;
using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using System.Text.Json;

namespace Endatix.Core.Entities
{
    public class Tenant : BaseEntity, IAggregateRoot
    {
        private readonly List<Form> _forms = [];
        private readonly List<FormDefinition> _formDefinitions = [];
        private readonly List<Submission> _submissions = [];
        private string? _slackSettingsJson;
        private SlackSettings? _slackSettings;

        private Tenant() { } // For EF Core

        public Tenant(string name, string? description = null)
        {
            Guard.Against.NullOrEmpty(name, nameof(name));
            
            Name = name;
            Description = description;
        }

        public string Name { get; private set; }
        public string? Description { get; private set; }
        public string? SlackSettingsJson 
        { 
            get => _slackSettingsJson;
            private set
            {
                _slackSettingsJson = value;
                _slackSettings = null; // Clear cached settings
            }
        }

        [NotMapped]
        public SlackSettings SlackSettings
        {
            get => _slackSettings ??= DeserializeSlackSettings();
        }

        public IReadOnlyCollection<Form> Forms => _forms.AsReadOnly();
        public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();
        public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();
        
        [NotMapped]
        public ICollection<User> Users { get; set; } = new List<User>();

        public void UpdateSlackSettings(SlackSettings settings)
        {
            _slackSettings = settings;
            SlackSettingsJson = JsonSerializer.Serialize(settings);
        }

        private SlackSettings DeserializeSlackSettings()
        {
            if (string.IsNullOrEmpty(SlackSettingsJson))
            {
                return new SlackSettings { Active = false };
            }

            return JsonSerializer.Deserialize<SlackSettings>(SlackSettingsJson) ?? 
                   new SlackSettings { Active = false };
        }
    }
}
