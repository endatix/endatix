# Endatix.Modules.Personalization

Audience personalization for Endatix: per-form properties and people, tenant match key, and
(later) import and personalised links.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET.

## Registration

Gated by `Endatix:FeatureFlags:PersonalizationModule` (off by default). Requires PostgreSQL.
`EndatixBuilder.UseDefaults()` calls `UseModule(PersonalizationModule.Instance)`.

## Schema

Database schema: `personalization`

| Table | Purpose |
|-------|---------|
| `AudienceSettings` | One row per tenant: match key (`email` / `external_id`) |
| `AudienceProperties` | Property definitions per form (`VariableName` immutable) |
| `AudienceMembers` | One person per tenant + normalized identifier |
| `AudienceMemberships` | Person on one form |
| `AudiencePropertyValues` | Property cells on a membership |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |

Permission: `Forms.Edit`.
