
# Endatix Platform

[![Build & Test](https://github.com/endatix/endatix/actions/workflows/build-ci.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/build-ci.yml)
[![Release](https://github.com/endatix/endatix/actions/workflows/release.yml/badge.svg)](https://github.com/endatix/endatix/actions/workflows/release.yml)
[![Coverage Status](https://coveralls.io/repos/github/endatix/endatix/badge.svg?branch=main)](https://coveralls.io/github/endatix/endatix?branch=main)

## What is Endatix?

Endatix Platform is an open-source data collection and management library for .NET.

It is designed for building secure, scalable, and integrated form-centric applications that work with [SurveyJS](https://github.com/surveyjs/survey-library). Endatix empowers business users with advanced workflows, automation, and meaningful insights.

The platform provides the necessary backend solution for end-to-end data management:
- defining and managing complex SurveyJS forms
- integrating them into software products
- collecting and storing the data
- processing the collected data

Endatix can be seamlessly integrated into a software product or used as a standalone product.

## Table of Contents
- [Design and dependencies](#design-and-dependencies)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [License](#license)
- [Contact](#contact)
- [Other resources](#other-resources)

## Design and dependencies

The code base consists of .NET projects having the following brief descriptions and internal dependencies:

* **Endatix.Api** - the web API endpoints. Depends on Endatix.Core and Endatix.Infrastructure
* **Endatix.Core** - the core application domain - entities, aggregate roots, domain events, use case and etc.. No dependencies
* **Endatix.Framework** - core framework project to expose common customization and extensibility points. To be used by all modules. No dependencies
* **Endatix.Extensions.Hosting** - Easy to use utilities for web hosting Endatix. Depends on Endatix.Framework, Endatix.Infrastructure & Endatix.SqlServer
* **Endatix.Infrastructure** - main implementations of the Core project. Deals with 3rd party integrations. Depends on Endatix.Core & Endatix.Framework
* **Endatix.SqlServer**   - implementation of MS SQL specific database logic. Depends on Endatix.Infrastructure
* **Endatix.WebHost**  - default app host project. Has zero code. Shows how endatix can be hosted and is used for debugging and testing the application. Depends on Endatix.Extensions.Hosting & Endatix.Api

<img width="636" alt="image" src="https://github.com/user-attachments/assets/9441264f-fd24-44c6-b5be-ebfb2f04ab31">

## Requirements

* .NET 9.0
* Entity Framework Core
* MS SQL or Azure SQL
* Node v20.12.2

## Installation

Endatix is a .NET product that can run on any operating system that supports .NET. To set up the Endatix Platform, follow these steps:

```bash
# Clone the repository
git clone https://github.com/endatix/endatix.git

# Navigate to the project directory
cd endatix

# Build the project
dotnet build
```

You will need to ensure that you have .NET installed on your machine. [Download and install .NET](https://dotnet.microsoft.com/download).

## Usage

See Endatix Documentation for details. (available soon)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact

For questions, issues, or contributions, you can reach out through the following channels:
- **GitHub Issues**: [Open an issue](https://github.com/endatix/endatix/issues) on our GitHub repository for bug reports, feature requests, or general inquiries.
- **Email**: Drop us a line at  [info@endatix.com](mailto:info@endatix.com) to directly contact our team if you prefer.

We recommend using GitHub Issues for public inquiries, bug reports, and feature requests, as it allows others to see and participate in the discussion. Email can be used for more private or specific queries.

## Other resources

If you wish to learn more about Endatix and what you can do with it, please visit:
- **Endatix Website**: https://endatix.com
- **Endatix Documentation**: Documentation website (available soon)
