# Endatix Data Platform #

The open-source data collection and management library for .NET.

Build secure, scalable, and integrated form-centric applications that work with [SurveyJS](https://github.com/surveyjs/survey-library) or any other JSON-based front-end. Empower business users with advanced workflows, automation and meaningful insights.

## What is this repository for?

* Provides server-side storage for form definitions and submissions.
* Version 0.1

## Requirements

* .NET 8.0
* Entity Framework Core
* MS SQL or Azure SQL
* Node v20.12.2

## Local Setup

### .NET Application
1. `dotnet restore` - to restore the dependencies
1. If you have HTTPS certificate error, run `dotnet dev-certs https` to issue a certificate and then `dotnet dev-certs https --trust` to trust the local HTTPS certs
1. Run the main `SampleWebApp` project using the CLI or your IDE
