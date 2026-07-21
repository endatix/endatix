<a href="https://endatix.com"><img width="100%" src="assets/images/endatix-api-banner.png" alt="Self-Hosted Alternative to SaaS Form and Survey Platforms" /></a>
<br />
<br />

# Endatix API

[![Build, Test & Release](https://github.com/endatix/endatix/actions/workflows/ci.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/ci.yml)
[![Publish Stable Artifacts](https://github.com/endatix/endatix/actions/workflows/release-artifacts.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/release-artifacts.yml)
[![Coverage Status](https://coveralls.io/repos/github/endatix/endatix/badge.svg?branch=main)](https://coveralls.io/github/endatix/endatix?branch=main)

## What is Endatix?

Endatix is a free form management backend API designed to integrate with the [SurveyJS](https://github.com/surveyjs/survey-library) frontend library.

It provides REST API endpoints for CRUD operations and the management of forms, templates, submissions, themes, custom form fields, and more. It can be used to build self-hosted or SaaS solutions that focus on collecting information from [humans](https://en.wikipedia.org/wiki/Human) in industries such as market research, legal, insurance, finance, education, healthcare, and more.

This API project is the core of the [Endatix Hub](https://github.com/endatix/endatix-hub) form management system for business users, offering a complete UI, seamless integration with the [SurveyJS Creator](https://github.com/surveyjs/survey-creator) form-building tool, and an [AI assistant](https://www.youtube.com/watch?v=aX_Hm4WYsEE).

For more information visit https://endatix.com

## Table of Contents
- [Features](#features)
- [Tech Stack](#requirements)
- [Supported Environments](#supported-environments)
- [Installation](#installation)
- [Usage](#usage)
- [Form Management Database Schema](#form-management-database-schema)
- [Codebase Organization and Dependencies](#codebase-organization-and-dependencies)
- [Software Design Approach](#software-design-approach)
- [License](#license)
- [Contact and Resources](#contact-and-resources)

## Features

* **Form Versioning** (Allows a form to be modified after it has started collecting submissions)
* **Form Access Control** (Forms can be publically accessible or password-protected) 
* **Form Lifecycle Management** (draft vs. published state)
* **Form Templates**
* **Data lists** (Reusable lists of choices for dropdowns that supoort lazy-loading)
* **Themes** (Based on SurveyJS [Themes and Styles](https://surveyjs.io/form-library/documentation/manage-default-themes-and-styles))
* **Partial Submissions** (Users can resume incomplete submissions)
* **Prefilled forms**
* **File storage for form-submitted documents, images, audio recordings, and videos** (Providers for Azure Blob storage, Amazon S3, or self-hosted RustFS)
* **Folders** (Users can organize forms in folders with access control)
* **Submission metadata** (Including completion status, date/time started, and date/time completed)
* **One submission per respondent** (Optional validation for non-anonymous forms and surveys)
* **Webhooks** (Support for *submission completed*, *form created*, *form updated*, and *form deleted* events)
* **reCAPTCHA support**
* **Email Notifications** (Sendgrid, Mailgun, and SMTP connectors)
* **Database-stored Custom Question Types** (SurveyJS [specialized](https://surveyjs.io/form-library/documentation/customize-question-types/create-specialized-question-types) or [composite](https://surveyjs.io/form-library/documentation/customize-question-types/create-composite-question-types) custom question code can be added at runtime)
* **Multitenancy** (ORM-enforced tenant isolation)
* **Basic Authentication**
* **Role Based Access Control**
* **Single-Sign-On** (Supports Keycloak and other [OAuth 2.0](https://oauth.net/2/) implementations)

## Tech Stack

* .NET 10.0 (formerly .NET Core)
* Entity Framework Core
* PostgreSQL, MS SQL, or Azure SQL

## Supported Environments

<img width="480" alt="image" src="assets/images/environments.png"><br>

Endatix runs on any server or workstation that supports [.NET 10.0 (formerly .NET Core)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), including **Linux**, **Windows**, and **macOS**.

It can be deployed to on-premise servers, cloud environments such as **Azure**, **AWS**, or **Google Cloud**, and also runs in [**Docker Containers**](https://hub.docker.com/u/endatix) for simplified setup and scaling.

## Installation

To set up the Endatix API ensure you have [.NET 10.0 SDK or Runtime (formerly .NET Core)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed and follow these steps:

```bash
# Clone the repository
git clone https://github.com/endatix/endatix.git

# Navigate to the project directory
cd endatix

# Build the project
dotnet build
```

## Usage

The following React example provides a very basic demonstration of loading a SurveyJS form and saving a submission through the Endatix API. For a complete API reference and implementation guides visit our [documentation portal](https://docs.endatix.com).

```TypeScript
import React, { useEffect, useState } from "react";
import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/survey-core.css'

export default function App() {
  const [survey, setSurvey] = useState(null);

  useEffect(() => {
    fetch("/api/forms/123456789/definition") // <-- Fetch form with id = 123456789
      .then(res => res.json())
      .then(data => {
        const json = JSON.parse(data.jsonData); // <-- Extract the SurveyJS form JSON
        const surveyModel = new Model(json);

        surveyModel.onComplete.add((sender) => { 
          fetch("/api/forms/123456789/submissions", { // <-- Create a new submission record
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              isComplete: true,
              currentPage: 0,
              jsonData: JSON.stringify(sender.data), // <-- Set the SurveyJS submission JSON
              metadata: "{}",
              reCaptchaToken: ""
            })
          })
          .then(res => res.json())
          .then(resp => console.log("Submission response:", resp))
          .catch(err => console.error("Submission error:", err));
        });

        setSurvey(surveyModel);
      });
  }, []);

  if (!survey) return <div>Loading...</div>;

  return <Survey model={survey} />;
}
```

## Database Schemas

### Form Management

This schema supports the main business logic of the form management platform.

```mermaid
erDiagram
    "public.CustomQuestions" {
        bigint Id PK
        character_varying_100_ Name
        text Description
        jsonb JsonData
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
    }
    "public.CustomQuestions" }o--|| "public.Tenants" : FK_CustomQuestions_Tenants_TenantId

    "public.DataListItems" {
        bigint Id PK
        bigint DataListId FK
        character_varying_100_ Label
        character_varying_100_ Value
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }
    "public.DataListItems" }o--|| "public.DataLists" : FK_DataListItems_DataLists_DataListId

    "public.DataLists" {
        bigint Id PK
        character_varying_100_ Name
        character_varying_500_ Description
        boolean IsActive
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
        character_varying_100_ NormalizedName
    }
    "public.DataLists" }o--|| "public.Tenants" : FK_DataLists_Tenants_TenantId

    "public.EmailTemplates" {
        bigint Id PK
        character_varying_100_ Name UK
        character_varying_200_ Subject
        text HtmlContent
        text PlainTextContent
        character_varying_255_ FromAddress
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }

    "public.Folders" {
        bigint Id PK
        character_varying_100_ Name
        character_varying_100_ NormalizedName
        character_varying_128_ UrlSlug
        character_varying_500_ Description
        boolean IsActive
        boolean Immutable
        jsonb Metadata
        bigint ParentFolderId FK
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
    }
    "public.Folders" }o..|| "public.Folders" : FK_Folders_Folders_ParentFolderId
    "public.Folders" }o--|| "public.Tenants" : FK_Folders_Tenants_TenantId

    "public.FormDefinitions" {
        bigint Id PK
        boolean IsDraft
        jsonb JsonData
        bigint FormId FK
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
    }
    "public.FormDefinitions" }o--|| "public.Forms" : FK_FormDefinitions_Forms_FormId
    "public.FormDefinitions" }o--|| "public.Tenants" : FK_FormDefinitions_Tenants_TenantId

    "public.FormDependencies" {
        bigint Id PK
        bigint FormId FK
        character_varying_100_ DependencyIdentifier
        character_varying_32_ DependencyType
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
    }
    "public.FormDependencies" }o--|| "public.Forms" : FK_FormDependencies_Forms_FormId
    "public.FormDependencies" }o--|| "public.Tenants" : FK_FormDependencies_Tenants_TenantId

    "public.FormTemplates" {
        bigint Id PK
        character_varying_100_ Name
        text Description
        jsonb JsonData
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
        bigint FolderId FK
    }
    "public.FormTemplates" }o..|| "public.Folders" : FK_FormTemplates_Folders_FolderId
    "public.FormTemplates" }o--|| "public.Tenants" : FK_FormTemplates_Tenants_TenantId

    "public.Forms" {
        bigint Id PK
        character_varying_100_ Name
        text Description
        boolean IsEnabled
        bigint ActiveDefinitionId FK, UK
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
        bigint ThemeId FK
        boolean IsPublic
        jsonb WebHookSettingsJson
        boolean LimitOnePerUser
        jsonb Metadata
        bigint FolderId FK
    }
    "public.Forms" }o..|| "public.Folders" : FK_Forms_Folders_FolderId
    "public.Forms" }o..|| "public.FormDefinitions" : FK_Forms_FormDefinitions_ActiveDefinitionId
    "public.Forms" }o--|| "public.Tenants" : FK_Forms_Tenants_TenantId
    "public.Forms" }o..|| "public.Themes" : FK_Forms_Themes_ThemeId

    "public.SubmissionVersions" {
        bigint Id PK
        bigint SubmissionId FK
        jsonb JsonData
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }
    "public.SubmissionVersions" }o--|| "public.Submissions" : FK_SubmissionVersions_Submissions_SubmissionId

    "public.Submissions" {
        bigint Id PK
        boolean IsComplete
        jsonb JsonData
        bigint FormId FK
        bigint FormDefinitionId FK
        integer CurrentPage
        jsonb Metadata
        timestamp_with_time_zone CompletedAt
        character_varying_64_ Token_Value
        timestamp_with_time_zone Token_ExpiresAt
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        character_varying_16_ Status
        bigint TenantId FK
        character_varying_64_ SubmittedBy
        boolean IsTestSubmission
        character_varying_256_ RestrictionKey UK
    }
    "public.Submissions" }o--|| "public.FormDefinitions" : FK_Submissions_FormDefinitions_FormDefinitionId
    "public.Submissions" }o--|| "public.Forms" : FK_Submissions_Forms_FormId
    "public.Submissions" }o--|| "public.Tenants" : FK_Submissions_Tenants_TenantId

    "public.TenantSettings" {
        bigint TenantId PK, FK
        integer SubmissionTokenExpiryHours
        boolean IsSubmissionTokenValidAfterCompletion
        jsonb SlackSettingsJson
        timestamp_with_time_zone ModifiedAt
        jsonb WebHookSettingsJson
        jsonb CustomExportsJson
        boolean RequireFolderAssignment
    }
    "public.TenantSettings" }o--|| "public.Tenants" : FK_TenantSettings_Tenants_TenantId

    "public.Tenants" {
        bigint Id PK
        character_varying_100_ Name
        text Description
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }

    "public.Themes" {
        bigint Id PK
        character_varying_100_ Name
        text Description
        jsonb JsonData
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
        bigint TenantId FK
    }
    "public.Themes" }o--|| "public.Tenants" : FK_Themes_Tenants_TenantId
```

### Identity

This schema supports authentication and authorization

```mermaid
erDiagram
    "identity.RoleClaims" {
        integer Id PK
        bigint RoleId FK
        text ClaimType
        text ClaimValue
    }
    "identity.RoleClaims" }o--|| "identity.Roles" : FK_RoleClaims_Roles_RoleId

    "identity.UserClaims" {
        integer Id PK
        bigint UserId FK
        text ClaimType
        text ClaimValue
    }
    "identity.UserClaims" }o--|| "identity.Users" : FK_UserClaims_Users_UserId

    "identity.EmailVerificationTokens" {
        bigint Id PK
        bigint UserId FK
        character_varying_64_ Token UK
        timestamp_with_time_zone ExpiresAt
        boolean IsUsed
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }
    "identity.EmailVerificationTokens" }o--|| "identity.Users" : FK_EmailVerificationTokens_Users_UserId

    "identity.Permissions" {
        bigint Id PK
        character_varying_100_ Name UK
        character_varying_500_ Description
        character_varying_100_ Category
        boolean IsSystemDefined
        boolean IsActive
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }

    "identity.RolePermissions" {
        bigint Id PK
        bigint RoleId FK
        bigint PermissionId FK
        timestamp_with_time_zone GrantedAt
        timestamp_with_time_zone ExpiresAt
        boolean IsActive
        timestamp_with_time_zone CreatedAt
        timestamp_with_time_zone ModifiedAt
        timestamp_with_time_zone DeletedAt
        boolean IsDeleted
    }
    "identity.RolePermissions" }o--|| "identity.Permissions" : FK_RolePermissions_Permissions_PermissionId
    "identity.RolePermissions" }o--|| "identity.Roles" : FK_RolePermissions_Roles_RoleId

    "identity.Roles" {
        bigint Id PK
        character_varying_500_ Description
        character_varying_256_ Name
        character_varying_256_ NormalizedName
        text ConcurrencyStamp
        boolean IsActive
        boolean IsSystemDefined
        bigint TenantId
    }

    "identity.UserLogins" {
        text LoginProvider
        text ProviderKey
        text ProviderDisplayName
        bigint UserId FK
    }
    "identity.UserLogins" }o--|| "identity.Users" : FK_UserLogins_Users_UserId

    "identity.UserRoles" {
        bigint UserId FK
        bigint RoleId FK
    }
    "identity.UserRoles" }o--|| "identity.Roles" : FK_UserRoles_Roles_RoleId
    "identity.UserRoles" }o--|| "identity.Users" : FK_UserRoles_Users_UserId

    "identity.UserTokens" {
        bigint UserId FK
        text LoginProvider
        text Name
        text Value
    }
    "identity.UserTokens" }o--|| "identity.Users" : FK_UserTokens_Users_UserId

    "identity.Users" {
        bigint Id PK
        text RefreshTokenHash
        timestamp_with_time_zone RefreshTokenExpireAt
        character_varying_256_ UserName
        character_varying_256_ NormalizedUserName UK
        character_varying_256_ Email
        character_varying_256_ NormalizedEmail
        boolean EmailConfirmed
        text PasswordHash
        text SecurityStamp
        text ConcurrencyStamp
        text PhoneNumber
        boolean PhoneNumberConfirmed
        boolean TwoFactorEnabled
        timestamp_with_time_zone LockoutEnd
        boolean LockoutEnabled
        integer AccessFailedCount
        bigint TenantId
    }


```

## Codebase Organization and Dependencies

The code base consists of .NET projects having the following brief descriptions and internal dependencies:

* **Endatix.Api** - the web API endpoints. Depends on Endatix.Core and Endatix.Infrastructure
* **Endatix.Core** - the core application domain - entities, aggregate roots, domain events, use case and etc.. No dependencies
* **Endatix.Framework** - core framework project to expose common customization and extensibility points. To be used by all modules. No dependencies
* **Endatix.Extensions.Hosting** - Easy to use utilities for web hosting Endatix. Depends on Endatix.Framework, Endatix.Infrastructure & Endatix.SqlServer
* **Endatix.Infrastructure** - main implementations of the Core project. Deals with 3rd party integrations. Depends on Endatix.Core & Endatix.Framework
* **Endatix.SqlServer**   - implementation of MS SQL specific database logic. Depends on Endatix.Infrastructure
* **Endatix.WebHost**  - default app host project. Has zero code. Shows how endatix can be hosted and is used for debugging and testing the application. Depends on Endatix.Extensions.Hosting & Endatix.Api

## Software Design Approach

- **Clean Architecture**: Enforces separation of concerns and dependency rules
- **Domain-Driven Design**: Supports rich domain models and business logic
- **Vertical Slice Architecture**: Organizes code by feature rather than technical concerns
- **API Configuration**: Simplified setup for RESTful APIs with versioning, Swagger, and CORS
- **Persistence**: Streamlined database configuration for SQL Server and PostgreSQL
- **Security**: Built-in JWT authentication and authorization
- **Logging**: Structured logging with Serilog
- **Health Checks**: Comprehensive health monitoring for applications and dependencies
- **Middleware**: Exception handling, request logging, and more

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact and Resources

For questions, issues, or contributions, you can reach out through the following channels:
- **GitHub Issues**: [Open an issue](https://github.com/endatix/endatix/issues) on our GitHub repository for bug reports, feature requests, or general inquiries.
- **Email**: Drop us a line at  [info@endatix.com](mailto:info@endatix.com) to directly contact our team if you prefer.
- **Endatix Website**: https://endatix.com
- **Official Documentation**: https://docs.endatix.com

We recommend using GitHub Issues for public inquiries, bug reports, and feature requests, as it allows others to see and participate in the discussion. Email can be used for more private or specific queries.