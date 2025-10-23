---
sidebar_position: 4
title: What is Endatix?
description: Learn about Endatix - the open-source backend for SurveyJS
---

# What is Endatix?

Endatix Platform is an open-source data collection and management library for .NET.

:::tip

If you just want to see how to install the platform and read about it later, please go to [Setup Using NuGet Package](/docs/getting-started/setup-nuget-package) or [Setup Using Docker](/docs/guides/docker-setup).

:::

It is designed for building secure, scalable, and integrated form-centric applications that work with [SurveyJS](https://surveyjs.io). Endatix empowers business users with advanced workflows, automation, and meaningful insights.

The platform provides the necessary backend solution for end-to-end data management:
- defining and managing complex SurveyJS forms
- integrating them into software products
- collecting and storing the data
- processing the collected data

Endatix can be seamlessly integrated into a software product or used as a standalone product.

## Core Concepts and Terminology

### Forms and Form Definitions
In Endatix, a **form** is the main entity that represents a data collection form. Each form has one active version that contains all the questions, validation rules, and logic for the form powered by SurveyJS JSON model. This active version is called **form definition**. 

### Submissions
A **submission** is a completed response to a form, containing the data provided by users.

[Learn more about forms and form definitions →](/docs/getting-started/architecture#key-entities)


## Architecture Overview

Endatix follows a modular architecture with several key components:

- **Core Library**: Provides the fundamental features and interfaces
- **Persistence Providers**: Handle data persistence
- **API Layer**: Exposes RESTful endpoints for form operations
- **Event System**: Manages triggers and subscriptions for workflow automation

[Learn more about the architecture →](/docs/getting-started/architecture#high-level-architecture)

## Key Differentiators

Unlike other form management systems, Endatix:

- Is completely open-source with MIT licensing
- Integrates natively with .NET Core applications
- Provides a storage-agnostic design
- Offers a developer-first approach with clean APIs
- Focuses specifically on enhancing SurveyJS functionality

## Use Case Scenarios

### Enterprise Form Management
Centralized management of forms across departments with standardized validation and workflow automation.

### Customer Support Portals
Dynamic forms for support requests with automatic routing and status tracking.

### Data Collection Applications
Create applications that require complex data collection, validation, and processing.

### Integration Platform
Use as middleware between user interfaces and backend systems for structured data collection.

[Learn more about the use case scenarios →](https://endatix.com/products)