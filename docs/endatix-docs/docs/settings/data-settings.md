---
sidebar_position: 1
title: Data Settings
---

# Configuring Data Settings in Endatix

Data settings are responbile for changing the behavior of the data persistance layer. They allow you to prevent or allow running data migrations of the DB schema

# Getting it done

To configure the data settings, add the following snippet to your appSettings.json file. Customize the values based on your requirements:

```json
"Endatix": {
    "Data": {
        "ApplyMigrations": true
    },
}

```

# Explanation

The settings are defined within the `"Data": {...}` section:

- **ApplyMigrations**: Determines whether to apply or not the database migrations.
    - Set to `true` to enable migrations.
    - Omit or set to `false` will prevent migrations from running unless explicitly set to true

:::warning Note on importance
 
:fire: Data settings are a critical part of your deployment strategy. You should carefully decide when and if data migrations are executed to ensure your database schema is properly managed according to your application's needs.

:::