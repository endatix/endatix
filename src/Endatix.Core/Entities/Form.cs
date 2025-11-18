using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Form : TenantEntity, IAggregateRoot
{
    private readonly List<FormDefinition> _formDefinitions = [];
    private string? _webHookSettingsJson;
    private WebHookConfiguration? _webHookSettings;

    private Form() { } // For EF Core

    public Form(long tenantId, string name, string? description = null, bool isEnabled = false, bool isPublic = true, string? webHookSettingsJson = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, null, "Form name cannot be null.");
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        IsPublic = isPublic;
        WebHookSettingsJson = webHookSettingsJson;
    }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsPublic { get; set; }

    public long? ActiveDefinitionId { get; private set; }
    public FormDefinition? ActiveDefinition { get; private set; }

    public long? ThemeId { get; private set; }
    public Theme? Theme { get; private set; }

    public string? WebHookSettingsJson
    {
        get => _webHookSettingsJson;
        private set
        {
            _webHookSettingsJson = value;
            _webHookSettings = null; // Clear cached settings
        }
    }

    [NotMapped]
    public WebHookConfiguration WebHookSettings
    {
        get => _webHookSettings ??= DeserializeWebHookSettings();
    }

    public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();

    public void SetActiveFormDefinition(FormDefinition formDefinition)
    {
        Guard.Against.Null(formDefinition, nameof(formDefinition));

        if (!_formDefinitions.Contains(formDefinition))
        {
            throw new InvalidOperationException("Cannot set a FormDefinition as active that doesn't belong to this form.");
        }

        ActiveDefinition = formDefinition;
    }

    public void AddFormDefinition(FormDefinition formDefinition, bool isActive = true)
    {
        _formDefinitions.Add(formDefinition);

        if (isActive && _formDefinitions.Count == 1)
        {
            SetActiveFormDefinition(formDefinition);
        }
    }
    
    public void SetTheme(Theme? theme)
    {
        Theme = theme;
        ThemeId = theme?.Id;
    }

    /// <summary>
    /// Updates the webhook configuration settings for this form.
    /// </summary>
    public void UpdateWebHookSettings(WebHookConfiguration? settings)
    {
        _webHookSettings = settings;
        WebHookSettingsJson = settings != null ? JsonSerializer.Serialize(settings) : null;
    }

    private WebHookConfiguration DeserializeWebHookSettings()
    {
        if (string.IsNullOrEmpty(WebHookSettingsJson))
        {
            return new WebHookConfiguration();
        }

        return JsonSerializer.Deserialize<WebHookConfiguration>(WebHookSettingsJson) ??
               new WebHookConfiguration();
    }

    public override void Delete()
    {
        if (!IsDeleted)
        {
            // Delete all related form definitions
            foreach (var definition in _formDefinitions)
            {
                definition.Delete();
            }

            // Delete the form itself
            base.Delete();
        }
    }
}
