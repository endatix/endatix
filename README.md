# Endatix Platform

[![Build & Test](https://github.com/endatix/endatix/actions/workflows/build-ci.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/build-ci.yml)
[![Release](https://github.com/endatix/endatix/actions/workflows/release.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/release.yml)
[![Coverage Status](https://coveralls.io/repos/github/endatix/endatix/badge.svg?branch=main)](https://coveralls.io/github/endatix/endatix?branch=main)

## What is Endatix?

Endatix is a free form management backend API designed to integrate with the <a href="https://github.com/surveyjs/survey-library" target="_blank">SurveyJS</a> frontend library.

It provides REST API endpoints for CRUD operations and the management of forms, templates, submissions, themes, custom form fields, and more. It can be used to build self-hosted or SaaS solutions that focus on collecting information from <a href="https://en.wikipedia.org/wiki/Human" target="_blank">humans</a> in industries such as market research, legal, insurance, finance, education, healthcare, and more.

This API project is the core of the <a href="https://endatix.com" target="_blank">Endatix Hub</a> form management system for business users, offering a complete UI, seamless integration with the <a href="https://github.com/surveyjs/survey-creator" target="_blank">SurveyJS Creator</a> form-building tool, and an <a href="https://www.youtube.com/watch?v=aX_Hm4WYsEE" target="_blank">AI assistant</a>.

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
* **Form Access Control** (Forms can be publically accessible or password protected) 
* **Form Lifecycle Management** (draft vs. published mode)
* **Form Templates**
* **Themes** (Based on the <a href="https://surveyjs.io/form-library/documentation/manage-default-themes-and-styles" target="_blank">SurveyJS Theme format</a>)
* **Partial Submissions** (Users can resume incomplete submissions)
* **Submission metadata** (Including completion status, date/time started, and date/time completed)
* **reCAPTCHA support**
* **Email Notifications** (Sendgrid and Mailgun connectors)
* **Database-stored Custom Question Types** (SurveyJS specialized or composite custom question code can be added at runtime)
* **Multitenancy** (ORM-enforced tenant isolation)
* **Basic Authentication**
* **Role Based Access Control**
* **Single-Sign-On** (Supports Keycloak and other <a href="https://oauth.net/2/" target="_blank">OAuth 2.0</a> implementations)

## Tech Stack

* .NET 9.0 (formerly .NET Core)
* Entity Framework Core
* PostgreSQL, MS SQL, or Azure SQL
* Node v20.12.2

## Supported Environments

Endatix runs on any server or workstation that supports <a href="https://dotnet.microsoft.com/en-us/download/dotnet/9.0" target="_blank">.NET 9.0 (formerly .NET Core)</a>, including **Linux**, **Windows**, and **macOS**.  
It can be deployed to on-premise servers, cloud environments such as **Azure**, **AWS**, or **Google Cloud**, and also runs in <a href="https://hub.docker.com/u/endatix" target="_blank">**Docker containers**</a> for simplified setup and scaling.

## Installation

To set up the Endatix API ensure you have <a href="https://dotnet.microsoft.com/en-us/download/dotnet/9.0" target="_blank">.NET 9.0 SDK or Runtime (formerly .NET Core)</a> installed and follow these steps:

```bash
# Clone the repository
git clone https://github.com/endatix/endatix.git

# Navigate to the project directory
cd endatix

# Build the project
dotnet build
```

## Usage

The following React example provides a very basic demonstration of loading a SurveyJS form and saving a submission through the Endatix API. For a complete API reference and implementation guides visit our <a href="https://docs.endatix.com" target="_blank">documentation portal</a>.

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

## Form Management Database Schema

<img width="1200" alt="image" src="assets/images/form-management-database-schema.png">

## Codebase Organization and Dependencies

The code base consists of .NET projects having the following brief descriptions and internal dependencies:

* **Endatix.Api** - the web API endpoints. Depends on Endatix.Core and Endatix.Infrastructure
* **Endatix.Core** - the core application domain - entities, aggregate roots, domain events, use case and etc.. No dependencies
* **Endatix.Framework** - core framework project to expose common customization and extensibility points. To be used by all modules. No dependencies
* **Endatix.Extensions.Hosting** - Easy to use utilities for web hosting Endatix. Depends on Endatix.Framework, Endatix.Infrastructure & Endatix.SqlServer
* **Endatix.Infrastructure** - main implementations of the Core project. Deals with 3rd party integrations. Depends on Endatix.Core & Endatix.Framework
* **Endatix.SqlServer**   - implementation of MS SQL specific database logic. Depends on Endatix.Infrastructure
* **Endatix.WebHost**  - default app host project. Has zero code. Shows how endatix can be hosted and is used for debugging and testing the application. Depends on Endatix.Extensions.Hosting & Endatix.Api

<img width="636" alt="image" src="assets/images/design-and-dependencies.png">

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