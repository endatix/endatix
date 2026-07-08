using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Forms are main entities in the Endatix platform. They are used to create and manage forms, surveys or questionnaires that can be used to collect data from users.
/// </summary>
public partial class Form : TenantEntity, IAggregateRoot, IHasFolder, IHasRevision
{
    private readonly List<FormDefinition> _formDefinitions = [];
    private readonly List<FormDependency> _dependencies = [];
    private string? _webHookSettingsJson;
    private WebHookConfiguration? _webHookSettings;

    private Form() { } // For EF Core

    public Form(long tenantId, string name, string? description = null, bool isEnabled = false, bool isPublic = true, bool limitOnePerUser = false, string? metadata = null, string? webHookSettingsJson = null, long? folderId = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(name, null, "Form name cannot be null.");
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        IsPublic = isPublic;
        LimitOnePerUser = isPublic ? false : limitOnePerUser;
        Metadata = metadata;
        WebHookSettingsJson = webHookSettingsJson;
        FolderId = folderId;
    }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsPublic { get; set; }
    public bool LimitOnePerUser { get; set; }
    public string? Metadata { get; set; }

    /// <summary>
    /// Monotonic aggregate revision, bumped on each business mutation. Distinct from form-definition
    /// versioning (<see cref="FormDefinitions"/>): it advances on every state change (rename, enable
    /// toggle, folder move, …), not only schema edits. Carried in integration event payloads so an
    /// order-sensitive consumer (e.g. a future audit log) can reconstruct order or detect gaps.
    /// Increment wiring lands with the event-raising work (Phase 5).
    /// </summary>
    public long Revision { get; private set; } = 1;

    public long? ActiveDefinitionId { get; private set; }
    public FormDefinition? ActiveDefinition { get; private set; }

    public long? ThemeId { get; private set; }
    public Theme? Theme { get; private set; }

    /// <inheritdoc />
    public long? FolderId { get; private set; }
    public Folder? Folder { get; set; }

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
    public IReadOnlyCollection<FormDependency> Dependencies => _dependencies.AsReadOnly();

    public void SetActiveFormDefinition(FormDefinition formDefinition)
    {
        Guard.Against.Null(formDefinition, nameof(formDefinition));

        if (!_formDefinitions.Contains(formDefinition))
        {
            throw new InvalidOperationException("Cannot set a FormDefinition as active that doesn't belong to this form.");
        }

        if (ActiveDefinition is not null && ReferenceEquals(ActiveDefinition, formDefinition))
        {
            return;
        }

        if (ActiveDefinition is not null
            && ActiveDefinition.Id == formDefinition.Id
            && formDefinition.Id != default)
        {
            ActiveDefinition = formDefinition;
            return;
        }

        ActiveDefinition = formDefinition;

        if (!formDefinition.IsDraft)
        {
            RaiseActiveDefinitionUpdated(formDefinition);
        }
    }

    public void AddFormDefinition(FormDefinition formDefinition, bool isActive = true)
    {
        _formDefinitions.Add(formDefinition);

        if (isActive && _formDefinitions.Count == 1)
        {
            SetActiveFormDefinition(formDefinition);
        }
    }

    /// <inheritdoc />
    public bool CanMoveToFolder(long? folderId) => !IsDeleted;

    /// <inheritdoc />
    public bool MoveToFolder(long? folderId)
    {
        if (!CanMoveToFolder(folderId))
        {
            return false;
        }

        FolderId = folderId;
        return true;
    }

    /// <inheritdoc />
    public bool ClearFolder() => MoveToFolder(null);

    public void SetTheme(Theme? theme)
    {
        Theme = theme;
        ThemeId = theme?.Id;
    }

    /// <summary>Advances the aggregate revision. Call from domain mutations that raise integration events.</summary>
    public void IncrementRevision() => Revision++;

    /// <summary>
    /// Raises the <c>form.created</c> integration event (captured to the outbox → webhook). Call once after the
    /// form and its active definition are set up, before saving. Creation is revision 1, so this does not bump.
    /// </summary>
    public void RaiseCreated() => RegisterDomainEvent(new FormCreatedEvent(this));

    /// <summary>
    /// Updates the active definition schema. When the JSON schema changes, bumps the revision and raises
    /// <c>form.definition.updated</c> for reporting schema compilation.
    /// </summary>
    public void UpdateActiveDefinitionSchema(string? jsonData, bool? isDraft = null)
    {
        Guard.Against.Null(ActiveDefinition);

        var schemaChanged = jsonData is not null
            && !string.Equals(ActiveDefinition.JsonData, jsonData, StringComparison.Ordinal);

        ActiveDefinition.UpdateSchema(jsonData);
        ActiveDefinition.UpdateDraftStatus(isDraft);

        if (schemaChanged)
        {
            RaiseActiveDefinitionUpdated(ActiveDefinition);
        }
    }

    /// <summary>
    /// Updates the editable form details, bumps the revision and raises the <c>form.updated</c> integration
    /// event (captured to the outbox → webhook) in a single step — so a caller can't mutate the form and forget
    /// the revision bump/event. The enabled-state change has its own method and event (see <see cref="SetEnabled"/>).
    /// </summary>
    public void UpdateDetails(string name, string? description, bool isPublic, bool limitOnePerUser, string? metadata)
    {
        Guard.Against.NullOrEmpty(name, null, "Form name cannot be null.");
        Name = name;
        Description = description;
        IsPublic = isPublic;
        LimitOnePerUser = limitOnePerUser;
        Metadata = metadata;
        RegisterRevisedDomainEvent(new FormUpdatedEvent(this));
    }

    /// <summary>
    /// Sets the enabled state. On an actual change it bumps the revision and raises
    /// <c>form.enabled_state_changed</c>; when the value is unchanged it is a no-op (no event).
    /// </summary>
    public void SetEnabled(bool isEnabled)
    {
        if (IsEnabled == isEnabled)
        {
            return;
        }

        IsEnabled = isEnabled;
        RegisterRevisedDomainEvent(new FormEnabledStateChangedEvent(this, isEnabled));
    }

    private void RegisterRevisedDomainEvent(DomainEventBase domainEvent)
    {
        IncrementRevision();
        RegisterDomainEvent(domainEvent);
    }

    private void RaiseActiveDefinitionUpdated(FormDefinition formDefinition) =>
        RegisterRevisedDomainEvent(new FormDefinitionUpdatedEvent(this, formDefinition));

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

            _dependencies.Clear();

            // Delete the form itself
            base.Delete();

            RegisterRevisedDomainEvent(new FormDeletedEvent(this));
        }
    }
}
