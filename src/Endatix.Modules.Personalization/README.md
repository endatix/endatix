# Endatix.Modules.Personalization

Audience personalization for Endatix: per-form properties and people, tenant match key, and
(later) import and personalised links.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET.

## Registration

Gated by `Endatix:FeatureFlags:PersonalizationModule` (off by default). Requires PostgreSQL.
`EndatixBuilder.UseDefaults()` calls `UseModule(PersonalizationModule.Instance)`.

## Schema

Database schema: `audience`

| Table | Purpose |
|-------|---------|
| `Settings` | One row per tenant: match key (`email` / `external_id`). No row means `email` |
| `Properties` | Property definitions per form (`VariableName` immutable) |
| `Members` | One person per tenant + identifier. Emails are lower-cased; external ids keep their case |
| `Memberships` | Person on one form |
| `PropertyValues` | Property cells on a membership |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |

Permission: `Forms.Edit`.
