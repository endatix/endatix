---
sidebar_position: 2
---

# Design and Architecture

## Dependencies

The code base consists of .NET projects having the following brief descriptions and internal dependencies:

* **Endatix.Core** - The core application domain - entities, aggregate roots, domain events, use cases, etc. No dependencies
* **Endatix.Framework** - Core framework project to expose common customization and extensibility points. To be used by all modules. No dependencies
* **Endatix.Infrastructure** - Main implementations of the Core project. Deals with 3rd party integrations. Depends on Endatix.Core & Endatix.Framework
* **Endatix.Api** - The web API endpoints. Depends on Endatix.Core and Endatix.Infrastructure
* **Endatix.Api.Host** - A lightweight API host package that sets up a proper API environment. Depends on Endatix.Api and Endatix.Hosting
* **Endatix.Persistence.SqlServer** - Implementation of MS SQL specific database logic. Depends on Endatix.Infrastructure
* **Endatix.Persistence.PostgreSql** - Implementation of PostgreSQL specific database logic. Depends on Endatix.Infrastructure
* **Endatix.Hosting** - Main hosting infrastructure package that bootstraps the application with proper configuration. Depends on Endatix.Framework, Endatix.Infrastructure, Endatix.Api, Endatix.Persistence.SqlServer and Endatix.Persistence.PostgreSql
* **Endatix.WebHost** - Default app host project with minimal code. Shows how Endatix can be hosted and is used for debugging and testing the application. Depends on Endatix.Hosting

## High Level Architecture

```mermaid
flowchart TD
    subgraph subGraph0["Endatix Solution Architecture"]
        B["Endatix.Core"]
        D["Endatix.Framework"]
        C["Endatix.Infrastructure"]
        A["Endatix.Api"]
        H["Endatix.Api.Host"]
        F["Endatix.Persistence.SqlServer"]
        I["Endatix.Persistence.PostgreSql"]
        E["Endatix.Hosting"]
        G["Endatix.WebHost"]
    end
    C -- depends on --> B & D
    A -- depends on --> B & C
    F -- depends on --> C
    I -- depends on --> C
    H -- depends on --> A & E
    E -- depends on --> F & I & D & C & A
    G -- depends on --> E
```